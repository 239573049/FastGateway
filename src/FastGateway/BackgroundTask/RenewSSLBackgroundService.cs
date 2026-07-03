using Core.Entities.Core;
using FastGateway.Services;

namespace FastGateway.BackgroundTask;

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
            try
            {
                await using (var scope = serviceProvider.CreateAsyncScope())
                {
                    var configService = scope.ServiceProvider.GetRequiredService<ConfigurationService>();

                    // 查询所有需要续期的证书
                    // 自定义上传的证书不参与自动续期；泛域名证书只能通过 DNS-01 手动验证，无法自动续期
                    var certs = configService.GetCerts()
                        .Where(x => x.Type != CertType.Custom)
                        .Where(x => !x.Domain.StartsWith("*."))
                        .Where(x => x.NotAfter == null || (x.NotAfter < DateTime.Now.AddDays(15) && x.AutoRenew))
                        .ToArray();

                    var isRenew = false;

                    foreach (var cert in certs)
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

                    if (isRenew) CertService.InitCert(configService.GetCerts().ToArray());
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