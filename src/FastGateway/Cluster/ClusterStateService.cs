using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FastGateway.Cluster;

/// <summary>
///     集群状态持久化（data/cluster.json），模式与 ConfigurationService 一致：
///     内存单实例 + 原子落盘。
/// </summary>
public class ClusterStateService
{
    private readonly string _statePath;
    private readonly Lock _lockObject = new();
    private ClusterState _state;

    public ClusterStateService()
    {
        _statePath = Path.Combine(AppContext.BaseDirectory, "data", "cluster.json");

        var directory = Path.GetDirectoryName(_statePath);
        if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        if (File.Exists(_statePath))
        {
            try
            {
                var json = File.ReadAllText(_statePath);
                _state = JsonSerializer.Deserialize(json, ClusterJsonContext.Default.ClusterState)
                         ?? new ClusterState();
            }
            catch
            {
                _state = new ClusterState();
            }
        }
        else
        {
            _state = new ClusterState();
        }

        ClusterRelay.UpdateSnapshot(_state);
    }

    /// <summary>
    ///     返回状态深拷贝（序列化往返）：读取方（状态接口 5s 轮询、节点代理）在锁外
    ///     遍历 Nodes/Invites，返回内部实例会与 Update 的并发写产生撕裂读
    /// </summary>
    public ClusterState Get()
    {
        lock (_lockObject)
        {
            var json = JsonSerializer.Serialize(_state, ClusterJsonContext.Default.ClusterState);
            return JsonSerializer.Deserialize(json, ClusterJsonContext.Default.ClusterState) ?? new ClusterState();
        }
    }

    /// <summary>在锁内变更状态并原子落盘</summary>
    public void Update(Action<ClusterState> mutate)
    {
        lock (_lockObject)
        {
            mutate(_state);

            ClusterRelay.UpdateSnapshot(_state);

            try
            {
                var json = JsonSerializer.Serialize(_state, ClusterJsonContext.Default.ClusterState);
                var tempPath = _statePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _statePath, overwrite: true);
            }
            catch (Exception ex)
            {
                // 落盘失败仅记录，内存状态仍然生效
                Console.WriteLine($"集群状态保存失败：{ex}");
            }
        }
    }

    public ClusterNode? FindNode(string nodeId)
    {
        lock (_lockObject)
        {
            return _state.Nodes.FirstOrDefault(n => n.Id == nodeId);
        }
    }

    /// <summary>校验邀请令牌（自动清理过期邀请）</summary>
    public bool ValidateInvite(string token)
    {
        lock (_lockObject)
        {
            _state.Invites.RemoveAll(i => i.ExpiresAt < DateTime.Now);
            return _state.Invites.Any(i => TokenEquals(i.Token, token));
        }
    }

    /// <summary>消费邀请令牌：邀请码一次性有效，泄露后不能被反复用来接入节点</summary>
    public bool TryConsumeInvite(string token)
    {
        var consumed = false;
        Update(s =>
        {
            s.Invites.RemoveAll(i => i.ExpiresAt < DateTime.Now);
            var index = s.Invites.FindIndex(i => TokenEquals(i.Token, token));
            if (index < 0) return;

            s.Invites.RemoveAt(index);
            consumed = true;
        });
        return consumed;
    }

    public static bool TokenEquals(string? expected, string? actual)
    {
        return !string.IsNullOrEmpty(expected) && actual != null &&
               CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected),
                   Encoding.UTF8.GetBytes(actual));
    }
}
