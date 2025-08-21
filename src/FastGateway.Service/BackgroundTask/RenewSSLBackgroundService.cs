using FastGateway.Service.Services;

namespace FastGateway.Service.BackgroundTask;

/// <inheritdoc />
public class RenewSslBackgroundService(ILogger<RenewSslBackgroundService> logger, IServiceProvider serviceProvider)
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
                await using (var scope = serviceProvider.CreateAsyncScope())
                {
                    var configService = scope.ServiceProvider.GetRequiredService<ConfigurationService>();

                    // 查询所有需要续期的证书
                    var certs = configService.GetCerts()
                        .Where(x => x.NotAfter == null || x.NotAfter < DateTime.Now.AddDays(15) && x.AutoRenew)
                        .ToArray();

                    var isRenew = false;

                    foreach (var cert in certs)
                    {
                        try
                        {
                            var context = await CertService.RegisterWithLetsEncrypt(cert.Email);

                            await CertService.ApplyForCert(context, cert);

                            configService.UpdateCert(cert);

                            logger.LogInformation($"证书续期成功：{cert.Id} {cert.Domain}");

                            isRenew = true;
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, $"证书续期失败：{cert.Id}  {cert.Domain}");

                            configService.UpdateCert(cert);
                        }
                    }

                    if (isRenew)
                    {
                        CertService.InitCert(configService.GetCerts().ToArray());
                    }
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