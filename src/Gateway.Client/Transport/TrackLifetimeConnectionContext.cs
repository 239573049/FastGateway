using System.IO.Pipelines;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

namespace Gateway.Client.Transport;

/// <summary>
/// This exists solely to track the lifetime of the connection
/// </summary>
internal class TrackLifetimeConnectionContext(ConnectionContext connection) : ConnectionContext
{
    private readonly TaskCompletionSource _executionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task ExecutionTask => _executionTcs.Task;

    public override string ConnectionId
    {
        get => connection.ConnectionId;
        set => connection.ConnectionId = value;
    }

    public override IFeatureCollection Features => connection.Features;

    public override IDictionary<object, object?> Items
    {
        get => connection.Items;
        set => connection.Items = value;
    }

    public override IDuplexPipe Transport
    {
        get => connection.Transport;
        set => connection.Transport = value;
    }

    public override EndPoint? LocalEndPoint
    {
        get => connection.LocalEndPoint;
        set => connection.LocalEndPoint = value;
    }

    public override EndPoint? RemoteEndPoint
    {
        get => connection.RemoteEndPoint;
        set => connection.RemoteEndPoint = value;
    }

    public override CancellationToken ConnectionClosed
    {
        get => connection.ConnectionClosed;
        set => connection.ConnectionClosed = value;
    }

    public override void Abort()
    {
        connection.Abort();
    }

    public override void Abort(ConnectionAbortedException abortReason)
    {
        connection.Abort(abortReason);
    }

    public override ValueTask DisposeAsync()
    {
        _executionTcs.TrySetResult();
        return connection.DisposeAsync();
    }
}