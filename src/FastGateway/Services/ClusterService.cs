using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FastGateway.Cluster;
using FastGateway.Infrastructure;

namespace FastGateway.Services;

/// <summary>
///     分布式集群管理：主网关生成接入码、登记从节点并推送配置；从网关凭接入码一键加入。
/// </summary>
public static class ClusterService
{
    public static IEndpointRouteBuilder MapCluster(this IEndpointRouteBuilder app)
    {
        var cluster = app.MapGroup("/api/v1/cluster")
            .WithTags("集群")
            .WithDescription("分布式集群管理")
            .AddEndpointFilter<ResultFilter>()
            .RequireAuthorization()
            .WithDisplayName("集群");

        cluster.MapGet("state", (ClusterStateService stateService) =>
        {
            var state = stateService.Get();

            var dto = new ClusterStateDto
            {
                Role = state.Role,
                NodeName = state.NodeName,
                MasterEndpoint = state.MasterEndpoint,
                SyncedVersion = state.SyncedVersion,
                LastSyncTime = state.LastSyncTime,
                Connected = ClusterNodeAgentService.Connected
            };

            foreach (var node in state.Nodes)
            {
                var (lastSeen, syncedVersion) = ClusterHub.GetNodeStatus(node.Id);
                dto.Nodes.Add(new ClusterNodeDto
                {
                    Id = node.Id,
                    Name = node.Name,
                    Online = ClusterHub.IsNodeOnline(node.Id),
                    LastSeen = lastSeen,
                    SyncedVersion = syncedVersion,
                    RegisteredAt = node.RegisteredAt
                });
            }

            return dto;
        }).WithDescription("获取集群状态").WithDisplayName("获取集群状态");

        cluster.MapPost("invite", (ClusterStateService stateService, GenerateInviteInput input) =>
        {
            var state = stateService.Get();
            if (state.Role == ClusterRole.Worker)
                throw new ValidationException("本网关已作为从节点加入其它集群，无法生成接入码");

            var endpoint = input.Endpoint?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(endpoint) ||
                (!endpoint.StartsWith("http://") && !endpoint.StartsWith("https://")))
                throw new ValidationException("请填写本网关对外可达的管理地址（http:// 或 https:// 开头）");

            var invite = new ClusterInvite
            {
                Token = GenerateToken(),
                Endpoint = endpoint,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddHours(24)
            };

            stateService.Update(s =>
            {
                s.Role = ClusterRole.Master;
                s.Invites.RemoveAll(i => i.ExpiresAt < DateTime.Now);
                s.Invites.Add(invite);
            });

            var codeJson = JsonSerializer.Serialize(
                new ClusterInviteCode { Endpoint = endpoint, Token = invite.Token },
                AppJsonContext.Default.ClusterInviteCode);

            return new GenerateInviteResult
            {
                Code = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(codeJson)),
                Endpoint = endpoint,
                ExpiresAt = invite.ExpiresAt
            };
        }).WithDescription("生成节点接入码").WithDisplayName("生成节点接入码");

        cluster.MapPost("join", async (
            ClusterStateService stateService,
            IHttpClientFactory httpClientFactory,
            JoinClusterInput input) =>
        {
            var state = stateService.Get();
            if (state.Role == ClusterRole.Master && state.Nodes.Count > 0)
                throw new ValidationException("本网关是主网关且已有从节点，无法加入其它集群");
            if (state.Role == ClusterRole.Worker)
                throw new ValidationException("本网关已加入集群，请先退出当前集群");

            ClusterInviteCode? code;
            try
            {
                var json = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(input.Code.Trim()));
                code = JsonSerializer.Deserialize(json, AppJsonContext.Default.ClusterInviteCode);
            }
            catch
            {
                throw new ValidationException("接入码格式错误");
            }

            if (code == null || string.IsNullOrEmpty(code.Endpoint) || string.IsNullOrEmpty(code.Token))
                throw new ValidationException("接入码格式错误");

            var nodeName = string.IsNullOrWhiteSpace(input.NodeName)
                ? Environment.MachineName
                : input.NodeName.Trim();

            // 凭邀请令牌向主网关注册本节点
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsJsonAsync(
                    $"{code.Endpoint.TrimEnd('/')}/api/v1/cluster/register?token={Uri.EscapeDataString(code.Token)}",
                    new RegisterNodeInput { NodeName = nodeName },
                    AppJsonContext.Default.RegisterNodeInput);
            }
            catch (Exception ex)
            {
                throw new ValidationException($"无法连接主网关 {code.Endpoint}：{ex.Message}");
            }

            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new ValidationException($"主网关拒绝注册（{(int)response.StatusCode}）：{body}");

            var result = JsonSerializer.Deserialize(body, AppJsonContext.Default.RegisterNodeResult);
            if (result == null || string.IsNullOrEmpty(result.NodeId) || string.IsNullOrEmpty(result.NodeToken))
                throw new ValidationException("主网关返回的注册结果无效");

            stateService.Update(s =>
            {
                s.Role = ClusterRole.Worker;
                s.NodeId = result.NodeId;
                s.NodeName = nodeName;
                s.MasterEndpoint = code.Endpoint.TrimEnd('/');
                s.NodeToken = result.NodeToken;
                s.SyncedVersion = 0;
                s.LastSyncTime = null;
            });
            // NodeAgent 后台服务轮询到 Worker 角色后会立即建立同步连接
        }).WithDescription("加入集群").WithDisplayName("加入集群");

        cluster.MapPost("leave", (ClusterStateService stateService) =>
        {
            var state = stateService.Get();
            if (state.Role != ClusterRole.Worker)
                throw new ValidationException("本网关未加入任何集群");

            stateService.Update(s =>
            {
                s.Role = ClusterRole.Standalone;
                s.MasterEndpoint = null;
                s.NodeId = null;
                s.NodeToken = null;
            });

            ClusterNodeAgentService.RequestDisconnect();
        }).WithDescription("退出集群").WithDisplayName("退出集群");

        cluster.MapDelete("nodes/{id}", async (ClusterStateService stateService, string id) =>
        {
            stateService.Update(s => s.Nodes.RemoveAll(n => n.Id == id));
            await ClusterHub.DisconnectNodeAsync(id);
        }).WithDescription("移除从节点").WithDisplayName("移除从节点");

        cluster.MapPost("push", async (ClusterStateService stateService) =>
        {
            var state = stateService.Get();
            if (state.Role != ClusterRole.Master)
                throw new ValidationException("仅主网关可以推送配置");

            await ClusterHub.BroadcastAsync();
        }).WithDescription("立即推送配置到所有节点").WithDisplayName("立即推送配置");

        cluster.MapPost("dissolve", async (ClusterStateService stateService) =>
        {
            var state = stateService.Get();
            if (state.Role != ClusterRole.Master)
                throw new ValidationException("仅主网关可以解散集群");

            foreach (var node in state.Nodes.ToArray())
                await ClusterHub.DisconnectNodeAsync(node.Id);

            stateService.Update(s =>
            {
                s.Role = ClusterRole.Standalone;
                s.Nodes.Clear();
                s.Invites.Clear();
            });
        }).WithDescription("解散集群").WithDisplayName("解散集群");

        // ===== 以下为节点间内部端点（凭令牌鉴权，不走 JWT） =====

        app.MapPost("/api/v1/cluster/register", async (HttpContext context, ClusterStateService stateService) =>
        {
            var token = context.Request.Query["token"].ToString();
            if (string.IsNullOrEmpty(token) || !stateService.ValidateInvite(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("接入码无效或已过期");
                return;
            }

            var input = await context.Request.ReadFromJsonAsync(AppJsonContext.Default.RegisterNodeInput);
            if (input == null || string.IsNullOrWhiteSpace(input.NodeName))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("节点名称不能为空");
                return;
            }

            var nodeName = input.NodeName.Trim();
            if (stateService.Get().Nodes.Any(n => n.Name == nodeName))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync($"节点名称 {nodeName} 已存在，请换一个名称");
                return;
            }

            if (!stateService.TryConsumeInvite(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("接入码无效或已过期");
                return;
            }

            var node = new ClusterNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = nodeName,
                Token = GenerateToken(),
                RegisteredAt = DateTime.Now
            };

            stateService.Update(s =>
            {
                s.Role = ClusterRole.Master;
                s.Nodes.Add(node);
            });

            await context.Response.WriteAsJsonAsync(
                new RegisterNodeResult { NodeId = node.Id, NodeToken = node.Token },
                AppJsonContext.Default.RegisterNodeResult);
        }).AllowAnonymous();

        app.MapGet("/api/v1/cluster/ws", ClusterHub.HandleNodeConnectionAsync).AllowAnonymous();

        return app;
    }

    private static string GenerateToken()
    {
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
    }
}
