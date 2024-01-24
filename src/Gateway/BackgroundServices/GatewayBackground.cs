using System.Net.NetworkInformation;

namespace Gateway.BackgroundServices;

public class GatewayBackgroundService(
    GatewayService gatewayService,
    RequestLogService requestLogService,
    CertificateService certificateService,
    SettingService settingService,
    IFreeSql freeSql) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 数据库迁移
        freeSql.CodeFirst.SyncStructure(typeof(RouteEntity), typeof(ClusterEntity), typeof(CertificateEntity),
            typeof(RequestLog), typeof(StaticFileProxyEntity), typeof(SettingEntity));

        // 首次启动时更新配置
        await gatewayService.RefreshConfig();
        await certificateService.RefreshConfig();

        if (!await settingService.AnyAsync(Constant.Setting.LogRetentionTime))
        {
            await settingService.SetAsync(Constant.Setting.LogRetentionTime, 30, "日志保留最大天数", "单位（天），不能小于1天，建议设置30天记录。");
        }

        if (!await settingService.AnyAsync(Constant.Setting.MaxRequestBodySize))
        {
            await settingService.SetAsync(Constant.Setting.MaxRequestBodySize, 1024, "请求最大的Body", "设置请求体的最大大小单位（MB）！");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // 计算当前时间距离23:59 多少秒
            var now = DateTime.Now;

            // 创建一个当天的23:59的DateTime
            var endOfDay = new DateTime(now.Year, now.Month, now.Day, 23, 59, 0);

            // 计算时间差
            var timeDifference = endOfDay - now;

            // 将时间差转换成总毫秒
            var totalSeconds = timeDifference.TotalMilliseconds;

            Console.WriteLine($"距离今天23:59还有：{totalSeconds} ms");

            await Task.Delay((int)totalSeconds, stoppingToken);

            Console.WriteLine("开始清理日志");
            var sw = Stopwatch.StartNew();
            var result = await requestLogService.ClearLogAsync();
            sw.Stop();
            Console.WriteLine($"日志清理完成，耗时：{sw.ElapsedMilliseconds} ms; 删除日志：{result} 条;");
        }
    }

}