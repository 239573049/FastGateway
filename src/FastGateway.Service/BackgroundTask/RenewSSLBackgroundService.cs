using FastGateway.Service.DataAccess;
using FastGateway.Service.Services;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.BackgroundTask;

public class RenewSSLBackgroundService(ILogger<RenewSSLBackgroundService> logger, IServiceProvider serviceProvider)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("证书自动续期服务已启动");

        // 暂停1分钟
        await Task.Delay(1000 * 60, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();

                var dbContext = scope.ServiceProvider.GetRequiredService<MasterContext>();

                // 查询所有需要续期的证书
                var certs = await dbContext.Certs
                    .Where(x => x.NotAfter == null || x.NotAfter < DateTime.Now.AddDays(15) && x.AutoRenew)
                    .ToArrayAsync(stoppingToken);

                var isRenew = false;

                foreach (var cert in certs)
                {
                    try
                    {
                        var context = await CertService.RegisterWithLetsEncrypt(cert.Email);

                        await CertService.ApplyForCert(context, cert);

                        await dbContext.Certs.Where(x => x.Id == cert.Id)
                            .ExecuteUpdateAsync(x => x.SetProperty(y => y.RenewStats, cert.RenewStats)
                                .SetProperty(y => y.RenewTime, cert.RenewTime)
                                .SetProperty(y => y.NotAfter, cert.NotAfter)
                                .SetProperty(y => y.Expired, cert.Expired)
                                .SetProperty(y => y.Certs, cert.Certs), stoppingToken);

                        logger.LogInformation($"证书续期成功：{cert.Id} {cert.Domain}");

                        isRenew = true;
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, $"证书续期失败：{cert.Id}  {cert.Domain}");

                        await dbContext.Certs.Where(x => x.Id == cert.Id)
                            .ExecuteUpdateAsync(x => x.SetProperty(y => y.RenewStats, cert.RenewStats)
                                .SetProperty(y => y.RenewTime, cert.RenewTime)
                                .SetProperty(y => y.NotAfter, cert.NotAfter)
                                .SetProperty(y => y.Expired, cert.Expired)
                                .SetProperty(y => y.Certs, cert.Certs), stoppingToken);
                    }
                }

                if (isRenew)
                {
                    CertService.InitCert(await dbContext.Certs.ToArrayAsync(cancellationToken: stoppingToken));
                }

                // 每24小时检查一次
                await Task.Delay(1000 * 60 * 60 * 24, stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "证书自动续期服务异常");
            }
        }
    }
}