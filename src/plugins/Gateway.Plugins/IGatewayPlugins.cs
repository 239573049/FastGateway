namespace Gateway.Plugins;

public interface IGatewayPlugins
{
    
    /// <summary>
    /// 获取插件信息
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<PluginInfo> GetPluginInfoAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 初始化插件
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 开启插件
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 停止插件
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StopAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 卸载插件
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task UninitializeAsync(CancellationToken cancellationToken = default);
}