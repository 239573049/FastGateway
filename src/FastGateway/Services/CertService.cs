using Directory = System.IO.Directory;

namespace FastGateway.Services;

public static class CertService
{
    public static Dictionary<string, CertData> Certs { get; private set; } = [];

    public static async Task LoadCerts(IFreeSql freeSql)
    {
        var cert = (await freeSql.Select<Cert>()
                .Where(x => x.AutoRenew && x.Expired == false)
                .ToListAsync())
            .SelectMany(x => x.Certs);

        Certs = cert.ToDictionary(x => x.Domain, x => x);
    }

    [Authorize]
    public static async Task<ResultDto<PageResultDto<Cert>>> GetListAsync(int page, int pageSize,
        [FromServices] IFreeSql freeSql)
    {
        var total = await freeSql.Select<Cert>().CountAsync();

        var items = await freeSql.Select<Cert>()
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return ResultDto<PageResultDto<Cert>>.SuccessResult(new PageResultDto<Cert>(items, total));
    }

    [Authorize]
    public static async Task<Cert> GetAsync(IFreeSql freeSql, string id)
    {
        var cert = await freeSql.Select<Cert>().Where(x => x.Id == id)
            .FirstAsync();

        return cert;
    }

    [Authorize]
    public static async Task<ResultDto> CreateAsync(IFreeSql freeSql, CertInput input)
    {
        var certs = await freeSql.Select<Cert>()
            .ToListAsync();

        // 判断是否存在相同的域名
        if (certs.Any(c => c.Domains.Any(x => input.Domains.Contains(x))))
        {
            return ResultDto.ErrorResult("存在相同的域名");
        }

        var cert = new Cert
        {
            Id = Guid.NewGuid().ToString(),
            Domains = input.Domains,
            AutoRenew = input.AutoRenew,
            Email = input.Email,
            Issuer = "Let's Encrypt",
            RenewStats = RenewStats.None,
        };

        await freeSql.Insert<Cert>(cert)
            .ExecuteAffrowsAsync();

        return ResultDto.SuccessResult();
    }

    [Authorize]
    public static async Task<Cert> UpdateAsync(IFreeSql freeSql, Cert cert)
    {
        await freeSql.Update<Cert>()
            .Where(x => x.Id == cert.Id)
            .Set(x => x.AutoRenew, cert.AutoRenew)
            .Set(x => x.Email, cert.Email)
            .Set(x => x.Domains, cert.Domains)
            .Set(x => x.Certs, cert.Certs)
            .ExecuteAffrowsAsync();

        return cert;
    }

    [Authorize]
    public static async Task DeleteAsync(IFreeSql freeSql, string id)
    {
        await freeSql.Delete<Cert>()
            .Where(x => x.Id == id)
            .ExecuteAffrowsAsync();
    }

    /// <summary>
    /// 申请证书
    /// </summary>
    [Authorize]
    public static async Task<ResultDto> ApplyAsync([FromServices] IMemoryCache memoryCache,
        [FromServices] IFreeSql freeSql, string id)
    {
        var cert = await freeSql.Select<Cert>()
            .Where(x => x.Id == id)
            .FirstAsync();

        if (cert == null)
        {
            return ResultDto.ErrorResult("证书不存在");
        }

        var context = await RegisterWithLetsEncrypt(cert.Email);

        if (await ApplyForCert(memoryCache, context, cert))
        {
            await LoadCerts(freeSql);
        }

        await freeSql.Update<Cert>()
            .Set(x => x.RenewStats, cert.RenewStats)
            .Set(x => x.RenewTime, cert.RenewTime)
            .Set(x => x.NotAfter, cert.NotAfter)
            .Set(x => x.Expired, cert.Expired)
            .Set(x=>x.Certs, cert.Certs)
            .Where(x => x.Id == cert.Id)
            .ExecuteAffrowsAsync();

        return ResultDto.SuccessResult();
    }


    public static async ValueTask<bool> ApplyForCert(IMemoryCache memoryCache, AcmeContext context, Cert certItem)
    {
        certItem.ClearCerts();

        foreach (var domain in certItem.Domains)
        {
            var order = await context.NewOrder(new[] { domain });

            var authz = (await order.Authorizations()).First();
            var httpChallenge = await authz.Http();

            // 保存验证信息
            memoryCache.Set(httpChallenge.Token, httpChallenge.KeyAuthz, TimeSpan.FromMinutes(20));

            await Task.Delay(1000);

            var challenge = await httpChallenge.Validate();

            for (int i = 0; i < 50; i++)
            {
                if (challenge.Status == ChallengeStatus.Valid)
                {
                    break;
                }

                if (challenge.Status == ChallengeStatus.Invalid)
                {
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
                CommonName = domain
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
            var certFile = Path.Combine(certPath, $"{domain}.pfx");
            await File.WriteAllBytesAsync(certFile, pfx);

            // 更新证书信息
            certItem.NotAfter = DateTime.Now.AddMonths(3);
            certItem.RenewTime = DateTime.Now;
            certItem.Expired = false;
            certItem.RenewStats = RenewStats.Success;

            certItem.AddCert(new CertData
            {
                File = certFile,
                Domain = domain,
                Password = certPassword
            });
        }

        return true;
    }

    public static async ValueTask<AcmeContext> RegisterWithLetsEncrypt(string email)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "certs");

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
}