using Microsoft.Extensions.Caching.Memory;

namespace FastGateway.BackgroundServices;

public sealed class RenewSslBackgroundService(IServiceProvider serviceProvider, IMemoryCache memoryCache)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("证书自动续期启动成功，基于Let's Encrypt 的免费证书");

        using var scope = serviceProvider.CreateScope();

        var masterDbContext = scope.ServiceProvider.GetRequiredService<MasterDbContext>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var certs = await masterDbContext.Certs.Where(x => x.AutoRenew)
                .ToListAsync(cancellationToken: stoppingToken);

            // 如果证书过期或者快过期，就续期
            foreach (var certItem in certs.Where(x => x.NotAfter == null || x.NotAfter < DateTime.Now.AddDays(7)))
            {
                try
                {
                    // 申请证书，使用Let's Encrypt，需要先注册账户
                    var context = await CertService.RegisterWithLetsEncrypt(certItem.Email);

                    // 申请证书
                    await CertService.ApplyForCert(memoryCache, context, certItem);

                    // 保存证书信息
                    masterDbContext.Certs.Update(certItem);

                    await masterDbContext.SaveChangesAsync(stoppingToken);

                    // 成功以后需要刷新证书列表
                    await CertService.LoadCerts(masterDbContext);
                }
                catch (Exception e)
                {
                    certItem.RenewStats = RenewStats.Fail;
                    certItem.RenewTime = DateTime.Now;
                    certItem.NotAfter = DateTime.Now.AddDays(-1);
                    certItem.Expired = true;
                    certItem.ClearCerts();

                    masterDbContext.Certs.Update(certItem);

                    await masterDbContext.SaveChangesAsync(stoppingToken);

                    Console.WriteLine($"域名：{string.Join(';', certItem.Domains)} 证书续期失败：" + e.Message);
                }
            }

            // 等待12小时
            await Task.Delay(1000 * 60 * 60 * 12, stoppingToken);
        }
    }
}