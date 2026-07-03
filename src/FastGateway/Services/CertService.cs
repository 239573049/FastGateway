using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
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

    public static Cert? GetCert(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return null;

        var host = domain.Trim().ToLowerInvariant();

        // 精确匹配优先
        foreach (var pair in CertWebApplications)
            if (string.Equals(pair.Key, host, StringComparison.OrdinalIgnoreCase))
                return pair.Value;

        // 泛域名匹配：a.example.com -> *.example.com（仅匹配一级子域名）
        var index = host.IndexOf('.');
        if (index > 0 && index < host.Length - 1)
        {
            var wildcard = "*." + host[(index + 1)..];
            foreach (var pair in CertWebApplications)
                if (string.Equals(pair.Key, wildcard, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
        }

        return null;
    }

    /// <summary>
    /// 生成安全的证书文件名，处理泛域名（*）及其它非法文件名字符。
    /// </summary>
    private static string SafeCertFileName(string domain)
    {
        var name = domain.Replace("*", "_wildcard_");
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
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

        if (cert.Type == CertType.Custom) return ResultDto.CreateFailed("自定义上传的证书不支持自动申请，请重新上传证书文件");

        if (cert.Domain.StartsWith("*.")) return ResultDto.CreateFailed("泛域名证书请使用 DNS 验证方式申请");

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
            var certFile = Path.Combine(certPath, $"{SafeCertFileName(certItem.Domain)}.pfx");
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

    /// <summary>
    /// 准备 DNS-01 验证（用于泛域名等无法通过 HTTP-01 验证的场景）。
    /// 生成 _acme-challenge TXT 记录供用户手动添加到域名解析。
    /// </summary>
    public static async Task<ResultDto> PrepareDnsChallengeAsync(ConfigurationService configService, string id)
    {
        var cert = configService.GetCerts().FirstOrDefault(x => x.Id == id);
        if (cert == null) return ResultDto.CreateFailed("证书不存在");
        if (cert.Type == CertType.Custom) return ResultDto.CreateFailed("自定义上传的证书无需申请");
        if (string.IsNullOrWhiteSpace(cert.Email)) return ResultDto.CreateFailed("请先为该证书配置邮箱");

        try
        {
            var context = await RegisterWithLetsEncrypt(cert.Email);
            var order = await context.NewOrder(new[] { cert.Domain });
            var authz = (await order.Authorizations()).First();
            var dnsChallenge = await authz.Dns();
            var dnsTxt = context.AccountKey.DnsTxt(dnsChallenge.Token);

            // 泛域名 *.example.com 的验证记录为 _acme-challenge.example.com
            var baseDomain = cert.Domain.StartsWith("*.") ? cert.Domain[2..] : cert.Domain;
            var recordName = "_acme-challenge." + baseDomain;

            // 缓存订单地址，供 validate 步骤续用（30 分钟内有效）
            MemoryCache.Set($"dns-order:{cert.Id}", order.Location.ToString(), TimeSpan.FromMinutes(30));

            return ResultDto.CreateSuccess(new
            {
                recordName,
                recordType = "TXT",
                recordValue = dnsTxt
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return ResultDto.CreateFailed("获取 DNS 验证记录失败：" + e.Message);
        }
    }

    /// <summary>
    /// 校验 DNS-01 记录并签发证书。用户在添加 TXT 记录后调用。
    /// </summary>
    public static async Task<ResultDto> ValidateDnsChallengeAsync(ConfigurationService configService, string id)
    {
        var cert = configService.GetCerts().FirstOrDefault(x => x.Id == id);
        if (cert == null) return ResultDto.CreateFailed("证书不存在");

        if (!MemoryCache.TryGetValue($"dns-order:{cert.Id}", out var cached) || cached is not string orderUri)
            return ResultDto.CreateFailed("验证信息已过期，请重新获取 DNS 记录");

        try
        {
            var context = await RegisterWithLetsEncrypt(cert.Email);
            var order = context.Order(new Uri(orderUri));
            var authz = (await order.Authorizations()).First();
            var dnsChallenge = await authz.Dns();

            var challenge = await dnsChallenge.Validate();
            for (var i = 0; i < 30; i++)
            {
                if (challenge.Status == ChallengeStatus.Valid) break;
                if (challenge.Status == ChallengeStatus.Invalid)
                {
                    cert.RenewStats = RenewStats.Fail;
                    configService.UpdateCert(cert);
                    return ResultDto.CreateFailed("DNS 验证失败：" + (challenge.Error?.Detail ?? "请检查 TXT 记录是否已生效"));
                }

                await Task.Delay(5000);
                challenge = await dnsChallenge.Resource();
            }

            if (challenge.Status != ChallengeStatus.Valid)
                return ResultDto.CreateFailed("DNS 验证超时，请确认 TXT 记录已生效后重试");

            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var certChain = await order.Generate(new CsrInfo { CommonName = cert.Domain }, privateKey);

            const string certPassword = "Aa123456";
            var pfx = certChain.ToPfx(privateKey).Build("my-cert", certPassword);

            var certPath = Path.Combine(AppContext.BaseDirectory, "certs");
            if (!Directory.Exists(certPath)) Directory.CreateDirectory(certPath);
            var certFile = Path.Combine(certPath, $"{SafeCertFileName(cert.Domain)}.pfx");
            await File.WriteAllBytesAsync(certFile, pfx);

            cert.NotAfter = DateTime.Now.AddMonths(3);
            cert.RenewTime = DateTime.Now;
            cert.Expired = false;
            cert.Type = CertType.LetsEncrypt;
            cert.RenewStats = RenewStats.Success;
            cert.Certs = new CertData { File = certFile, Domain = cert.Domain, Password = certPassword };

            configService.UpdateCert(cert);
            MemoryCache.Remove($"dns-order:{cert.Id}");
            CertWebApplications[cert.Domain] = cert;
            Gateway.Gateway.InvalidateCertificate(cert.Domain);

            return ResultDto.CreateSuccess();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return ResultDto.CreateFailed("签发证书失败：" + e.Message);
        }
    }

    /// <summary>
    /// 上传用户自定义证书（支持 pfx/p12 与 pem 格式），统一转换为固定密码的 pfx 后落盘。
    /// </summary>
    public static async Task<ResultDto> UploadAsync(ConfigurationService configService, HttpRequest request)
    {
        if (!request.HasFormContentType)
            return ResultDto.CreateFailed("请使用 multipart/form-data 上传证书");

        var form = await request.ReadFormAsync();

        var domain = form["domain"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(domain))
            return ResultDto.CreateFailed("域名不能为空");

        var id = form["id"].ToString();
        var certType = form["certType"].ToString().Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(certType)) certType = "pfx";

        var existing = configService.GetCerts().FirstOrDefault(x => x.Id == id);
        var previousDomain = existing?.Domain;

        // 域名唯一性校验（排除自身）
        if (configService.GetCerts().Any(x => x.Domain == domain && x.Id != existing?.Id))
            return ResultDto.CreateFailed("域名已存在");

        // 统一使用固定密码保存，保持与自动申请证书一致，SNI 加载时无需用户密码
        const string storagePassword = "Aa123456";
        byte[] pfxBytes;

        try
        {
            if (certType == "pem")
            {
                var certFile = form.Files["file"];
                var keyFile = form.Files["keyFile"];
                if (certFile == null || certFile.Length == 0)
                    return ResultDto.CreateFailed("请上传证书文件（.pem/.crt）");
                if (keyFile == null || keyFile.Length == 0)
                    return ResultDto.CreateFailed("请上传证书私钥文件（.key）");

                var certPem = await ReadAllTextAsync(certFile);
                var keyPem = await ReadAllTextAsync(keyFile);

                using var fromPem = X509Certificate2.CreateFromPem(certPem, keyPem);
                pfxBytes = fromPem.Export(X509ContentType.Pfx, storagePassword);
            }
            else
            {
                var pfxFile = form.Files["file"];
                if (pfxFile == null || pfxFile.Length == 0)
                    return ResultDto.CreateFailed("请上传证书文件（.pfx/.p12）");

                var password = form["password"].ToString();
                var rawBytes = await ReadAllBytesAsync(pfxFile);

                // 导入后重新导出为固定密码的 pfx，并保留完整证书链
                var collection = new X509Certificate2Collection();
                collection.Import(rawBytes, password,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
                pfxBytes = collection.Export(X509ContentType.Pfx, storagePassword)!;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return ResultDto.CreateFailed("证书解析失败，请检查文件格式和密码是否正确");
        }

        // 重新加载以读取证书元数据并校验私钥
        using var certificate = new X509Certificate2(pfxBytes, storagePassword, X509KeyStorageFlags.EphemeralKeySet);
        if (!certificate.HasPrivateKey)
            return ResultDto.CreateFailed("上传的证书缺少私钥，无法用于 HTTPS");

        // 保存证书文件
        var certPath = Path.Combine(AppContext.BaseDirectory, "certs");
        if (!Directory.Exists(certPath)) Directory.CreateDirectory(certPath);
        var savePath = Path.Combine(certPath, $"{SafeCertFileName(domain)}.pfx");
        await File.WriteAllBytesAsync(savePath, pfxBytes);

        var target = existing ?? new Cert { CreateTime = DateTime.Now };
        target.Domain = domain;
        target.Type = CertType.Custom;
        target.AutoRenew = false;
        target.Expired = certificate.NotAfter < DateTime.Now;
        target.NotAfter = certificate.NotAfter;
        target.Issuer = certificate.GetNameInfo(X509NameType.SimpleName, true);
        target.RenewTime = DateTime.Now;
        target.RenewStats = RenewStats.Success;
        var email = form["email"].ToString();
        if (!string.IsNullOrWhiteSpace(email)) target.Email = email;
        target.Certs = new CertData
        {
            File = savePath,
            Domain = domain,
            Password = storagePassword
        };

        if (existing == null)
            configService.AddCert(target);
        else
            configService.UpdateCert(target);

        // 域名发生变更时，清理旧域名的内存证书与网关缓存，避免残留
        if (previousDomain != null && !string.Equals(previousDomain, domain, StringComparison.OrdinalIgnoreCase))
        {
            CertWebApplications.TryRemove(previousDomain, out _);
            Gateway.Gateway.InvalidateCertificate(previousDomain);
        }

        // 更新内存证书并清除网关缓存以立即生效
        CertWebApplications[domain] = target;
        Gateway.Gateway.InvalidateCertificate(domain);

        return ResultDto.CreateSuccess();
    }

    private static async Task<byte[]> ReadAllBytesAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return ms.ToArray();
    }

    private static async Task<string> ReadAllTextAsync(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        return await reader.ReadToEndAsync();
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

        cert.MapPost("upload",
                async (ConfigurationService configService, HttpRequest request) =>
                    await UploadAsync(configService, request))
            .WithDescription("上传自定义证书").WithDisplayName("上传自定义证书").WithTags("证书")
            .DisableAntiforgery();

        cert.MapPost("{id}/dns/prepare",
                async (ConfigurationService configService, string id) =>
                    await PrepareDnsChallengeAsync(configService, id))
            .WithDescription("获取DNS验证记录").WithDisplayName("获取DNS验证记录").WithTags("证书");

        cert.MapPost("{id}/dns/validate",
                async (ConfigurationService configService, string id) =>
                    await ValidateDnsChallengeAsync(configService, id))
            .WithDescription("校验DNS并签发证书").WithDisplayName("校验DNS并签发证书").WithTags("证书");

        return app;
    }
}