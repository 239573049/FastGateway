using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using FastGateway.Entities;
using FastGateway.Entities.Core;
using FastGateway.Service.DataAccess;
using FastGateway.Service.Dto;
using FastGateway.Service.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Directory = System.IO.Directory;

namespace FastGateway.Service.Services;

public static class CertService
{
    private static readonly ConcurrentDictionary<string, Cert> CertWebApplications = new();

    private static readonly IMemoryCache MemoryCache = new MemoryCache(new MemoryCacheOptions()
    {
        ExpirationScanFrequency = TimeSpan.FromMinutes(1)
    });


    public static async Task Challenge(HttpContext context, string token)
    {
        if (MemoryCache.TryGetValue(token, out var value))
        {
            context.Response.ContentType = "text/plain";

            await context.Response.WriteAsync(value.ToString());

            return;
        }

        context.Response.StatusCode = 404;
    }

    public static Cert? GetCert(string domain)
    {
        return CertWebApplications.TryGetValue(domain, out var cert) ? cert : null;
    }

    public static void InitCert(Cert[] certs)
    {
        foreach (var cert in certs)
        {
            CertWebApplications.TryAdd(cert.Domain, cert);
        }
    }

    public static async ValueTask<AcmeContext> RegisterWithLetsEncrypt(string email)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data/keys");

        var keyPath = Path.Combine(path, email + ".pem");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        if (File.Exists(keyPath))
        {
            var pemKey = await File.ReadAllTextAsync(keyPath);
            var accountKey = KeyFactory.FromPem(pemKey);
            var context = new AcmeContext(WellKnownServers.LetsEncryptV2, accountKey);
            var account = await context.Account();

            return context;
        }
        else
        {
            var context = new AcmeContext(WellKnownServers.LetsEncryptV2);
            // 创建新的账户，并同意服务条款
            var account = await context.NewAccount(email, true);

            // 保存账户密钥以便将来使用
            var pemKey = context.AccountKey.ToPem();

            await File.WriteAllTextAsync(keyPath, pemKey);

            return context;
        }
    }

    public static async Task<ResultDto> ApplyAsync(MasterContext masterContext, string id)
    {
        var cert = await masterContext.Certs.FirstOrDefaultAsync(x => x.Id == id);

        if (cert == null)
        {
            return ResultDto.CreateFailed("证书不存在");
        }

        if (!Gateway.Gateway.Has80Service)
        {
            return ResultDto.CreateFailed("请先80端口服务，再申请证书，否则无法验证域名");
        }

        var context = await RegisterWithLetsEncrypt(cert.Email);

        if (await ApplyForCert(context, cert))
        {
            await masterContext.Certs.Where(x => x.Id == id)
                .ExecuteUpdateAsync(i => i.SetProperty(x => x.Certs, x => cert.Certs)
                    .SetProperty(x => x.Expired, x => false)
                    .SetProperty(x => x.RenewStats, x => RenewStats.Success)
                    .SetProperty(x => x.NotAfter, x => cert.NotAfter)
                    .SetProperty(x => x.RenewTime, x => cert.RenewTime)
                );

            InitCert(await masterContext.Certs.Where(x => !x.Expired).ToArrayAsync());
        }

        return ResultDto.CreateSuccess();
    }


    public static async ValueTask<bool> ApplyForCert(AcmeContext context, Cert certItem)
    {
        try
        {
            var order = await context.NewOrder(new[] { certItem.Domain });

            var authz = (await order.Authorizations()).First();
            var httpChallenge = await authz.Http();

            // 保存验证信息
            MemoryCache.Set(httpChallenge.Token, httpChallenge.KeyAuthz, TimeSpan.FromMinutes(20));

            await Task.Delay(1000);

            var challenge = await httpChallenge.Validate();

            for (int i = 0; i < 50; i++)
            {
                Console.WriteLine($"{challenge.Url} | loop count: {i} | Status: {challenge.Status}");
                if (challenge.Status == ChallengeStatus.Valid)
                {
                    break;
                }

                if (challenge.Status == ChallengeStatus.Invalid)
                {
                    if (challenge.Error != null)
                    {
                        Console.WriteLine($"域名 {certItem.Domain} 验证失败！报错详情：{challenge.Error.Detail}");
                    }

                    throw new Exception("验证失败，请检查域名是否正确配置");
                }

                await Task.Delay(5000);
                // 更新验证状态
                challenge = await httpChallenge.Resource();
            }

            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var cert = await order.Generate(new CsrInfo
            {
                CountryName = "CA",
                State = "Ontario",
                Locality = "Toronto",
                Organization = "Certes",
                OrganizationUnit = "Dev",
                CommonName = certItem.Domain
            }, privateKey);

            var certPassword = "Aa123456";
            var pfxBuilder = cert.ToPfx(privateKey);
            var pfx = pfxBuilder.Build("my-cert", certPassword);


            // // 获取证书文件目录
            var certPath = Path.Combine(AppContext.BaseDirectory, "certs");
            if (!Directory.Exists(certPath))
            {
                Directory.CreateDirectory(certPath);
            }

            // 保存证书文件
            var certFile = Path.Combine(certPath, $"{certItem.Domain}.pfx");
            await File.WriteAllBytesAsync(certFile, pfx);

            // 更新证书信息
            certItem.NotAfter = DateTime.Now.AddMonths(3);
            certItem.RenewTime = DateTime.Now;
            certItem.Expired = false;
            certItem.RenewStats = RenewStats.Success;

            certItem.Certs = new CertData
            {
                File = certFile,
                Domain = certItem.Domain,
                Password = certPassword
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        return true;
    }

    public static IEndpointRouteBuilder MapCert(this IEndpointRouteBuilder app)
    {
        var cert = app.MapGroup("/api/v1/cert")
            .WithTags("证书")
            .WithDescription("证书管理")
            .RequireAuthorization()
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("证书");

        cert.MapPost(string.Empty, async (MasterContext dbContext, Cert cert) =>
        {
            if (string.IsNullOrWhiteSpace(cert.Domain))
            {
                throw new ValidationException("域名不能为空");
            }

            if (await dbContext.Certs.AnyAsync(x => x.Domain == cert.Domain))
            {
                throw new ValidationException("域名已存在");
            }

            cert.CreateTime = DateTime.Now;
            dbContext.Certs.Add(cert);

            await dbContext.SaveChangesAsync();
        }).WithDescription("创建证书").WithDisplayName("创建证书").WithTags("证书");

        cert.MapGet(string.Empty, async (MasterContext dbContext, int page, int pageSize) =>
            {
                var result = await dbContext.Certs
                    .OrderByDescending(x => x.CreateTime)
                    .Skip((page - 1) * pageSize)
                    .ToListAsync();

                var total = await dbContext.Certs.CountAsync();

                return new PagingDto<Cert>(total, result);
            })
            .WithDescription("获取证书列表")
            .WithDisplayName("获取证书列表")
            .WithTags("证书");

        cert.MapDelete("{id}",
            async (MasterContext dbContext, string id) =>
            {
                var cert = await dbContext.Certs.FirstOrDefaultAsync(x => x.Id == id);

                await dbContext.Certs.Where(x => x.Id == id).ExecuteDeleteAsync();

                CertWebApplications.TryRemove(cert.Domain, out _);
            }).WithDescription("删除证书").WithDisplayName("删除证书").WithTags("证书");

        cert.MapPut("{id}", async (MasterContext dbContext, string id, Cert cert) =>
        {
            if (string.IsNullOrWhiteSpace(cert.Domain))
            {
                throw new ValidationException("域名不能为空");
            }

            if (await dbContext.Certs.AnyAsync(x => x.Domain == cert.Domain && x.Id != id))
            {
                throw new ValidationException("域名已存在");
            }

            cert.Id = id;
            dbContext.Certs.Update(cert);

            await dbContext.SaveChangesAsync();
        }).WithDescription("更新证书").WithDisplayName("更新证书").WithTags("证书");


        cert.MapPost("{id}/apply", async (MasterContext dbContext, string id) => await ApplyAsync(dbContext, id))
            .WithDescription("申请证书").WithDisplayName("申请证书").WithTags("证书");

        return app;
    }
}