using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace FastGateway.Tunnels;

/// <summary>
///     客户端管理器
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public sealed class AgentClientManager : IEnumerable
{
    private readonly AgentStateChannel _clientStateChannel;
    private readonly ConcurrentDictionary<string, AgentClient> _dictionary = new();

    public AgentClientManager(AgentStateChannel clientStateChannel)
    {
        _clientStateChannel = clientStateChannel;
    }

    /// <inheritdoc />
    public int Count => _dictionary.Count;

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }


    /// <inheritdoc />
    public bool TryGetValue(string clientId, [MaybeNullWhen(false)] out AgentClient client)
    {
        return _dictionary.TryGetValue(clientId.ToLowerInvariant(), out client);
    }

    /// <summary>
    ///     添加客户端实例
    /// </summary>
    /// <param name="client">客户端实例</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask<bool> AddAsync(AgentClient client, CancellationToken cancellationToken)
    {
        var clientId = client.Id;
        if (_dictionary.TryRemove(clientId.ToLowerInvariant(), out var existClient)) await existClient.DisposeAsync();

        if (_dictionary.TryAdd(clientId.ToLowerInvariant(), client))
        {
            await _clientStateChannel.WriteAsync(client, true, cancellationToken);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     移除客户端实例
    /// </summary>
    /// <param name="client">客户端实例</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask<bool> RemoveAsync(AgentClient client, CancellationToken cancellationToken)
    {
        var clientId = client.Id;
        if (_dictionary.TryRemove(clientId.ToLowerInvariant(), out var existClient))
        {
            if (ReferenceEquals(existClient, client))
            {
                await _clientStateChannel.WriteAsync(client, false, cancellationToken);
                return true;
            }

            _dictionary.TryAdd(clientId, existClient);
        }

        return false;
    }


    /// <inheritdoc />
    public IEnumerator<AgentClient> GetEnumerator()
    {
        foreach (var keyValue in _dictionary) yield return keyValue.Value;
    }
}