using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using Core.Entities;
using Core.Entities.Core;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Directory = System.IO.Directory;

namespace FastGateway.Services;

public static class CertService
{
    private static readonly ConcurrentDictionary<string, Cert> CertWebApplications = new();

    private static readonly IMemoryCache MemoryCache = new MemoryCache(new MemoryCacheOptions
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
        foreach (var cert in certs) CertWebApplications.TryAdd(cert.Domain, cert);
    }

    public static async ValueTask<AcmeContext> RegisterWithLetsEncrypt(string email)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data/keys");

        var keyPath = Path.Combine(path, email + ".pem");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

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

    public static async Task<ResultDto> ApplyAsync(ConfigurationService configService, string id)
    {
        var cert = configService.GetCerts().FirstOrDefault(x => x.Id == id);

        if (cert == null) return ResultDto.CreateFailed("证书不存在");

        if (!Gateway.Gateway.Has80Service) return ResultDto.CreateFailed("请先80端口服务，再申请证书，否则无法验证域名");

        var context = await RegisterWithLetsEncrypt(cert.Email);

        if (await ApplyForCert(context, cert))
        {
            configService.UpdateCert(cert);
            InitCert(configService.GetCerts().Where(x => !x.Expired).ToArray());
            // 清除网关证书缓存以便新证书生效
            Gateway.Gateway.InvalidateCertificate(cert.Domain);
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

            for (var i = 0; i < 50; i++)
            {
                Console.WriteLine($"{challenge.Url} | loop count: {i} | Status: {challenge.Status}");
                if (challenge.Status == ChallengeStatus.Valid) break;

                if (challenge.Status == ChallengeStatus.Invalid)
                {
                    if (challenge.Error != null)
                        Console.WriteLine($"域名 {certItem.Domain} 验证失败！报错详情：{challenge.Error.Detail}");

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
            if (!Directory.Exists(certPath)) Directory.CreateDirectory(certPath);

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

        cert.MapPost(string.Empty, (ConfigurationService configService, Cert cert) =>
        {
            if (string.IsNullOrWhiteSpace(cert.Domain)) throw new ValidationException("域名不能为空");

            if (configService.GetCerts().Any(x => x.Domain == cert.Domain)) throw new ValidationException("域名已存在");

            cert.CreateTime = DateTime.Now;
            configService.AddCert(cert);
        }).WithDescription("创建证书").WithDisplayName("创建证书").WithTags("证书");

        cert.MapGet(string.Empty, (ConfigurationService configService, int page, int pageSize) =>
            {
                var allCerts = configService.GetCerts().OrderByDescending(x => x.CreateTime);
                var total = allCerts.Count();
                var result = allCerts
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new PagingDto<Cert>(total, result);
            })
            .WithDescription("获取证书列表")
            .WithDisplayName("获取证书列表")
            .WithTags("证书");

        cert.MapDelete("{id}",
            (ConfigurationService configService, string id) =>
            {
                var certItem = configService.GetCerts().FirstOrDefault(x => x.Id == id);
                if (certItem != null)
                {
                    configService.DeleteCert(id);
                    CertWebApplications.TryRemove(certItem.Domain, out _);
                    // 删除证书后移除网关缓存
                    Gateway.Gateway.InvalidateCertificate(certItem.Domain);
                }
            }).WithDescription("删除证书").WithDisplayName("删除证书").WithTags("证书");

        cert.MapPut("{id}", (ConfigurationService configService, string id, Cert cert) =>
        {
            if (string.IsNullOrWhiteSpace(cert.Domain)) throw new ValidationException("域名不能为空");

            if (configService.GetCerts().Any(x => x.Domain == cert.Domain && x.Id != id))
                throw new ValidationException("域名已存在");

            cert.Id = id;
            configService.UpdateCert(cert);
            // 更新证书后移除网关缓存
            Gateway.Gateway.InvalidateCertificate(cert.Domain);
        }).WithDescription("更新证书").WithDisplayName("更新证书").WithTags("证书");


        cert.MapPost("{id}/apply",
                async (ConfigurationService configService, string id) => await ApplyAsync(configService, id))
            .WithDescription("申请证书").WithDisplayName("申请证书").WithTags("证书");

        return app;
    }
}