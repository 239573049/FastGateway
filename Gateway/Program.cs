using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        builderContext.RequestTransforms.Add(new RequestFuncTransform((async context =>
        {
            // ������ͷ�е� x-Forwarded-For ����Ϊ�ͻ��˵���ʵIP
            if (context.ProxyRequest.Headers.TryGetValues("x-Forwarded-For", out var value))
            {
                context.ProxyRequest.Headers.Remove("x-Forwarded-For");
                context.ProxyRequest.Headers.Add("x-Forwarded-For",
                    context.HttpContext.Connection.RemoteIpAddress?.ToString());
            }
            else
            {
                context.ProxyRequest.Headers.Add("x-Forwarded-For",
                    context.HttpContext.Connection.RemoteIpAddress?.ToString());
            }

            await Task.CompletedTask;
        })));


        builderContext.ResponseTransforms.Add(new ResponseFuncTransform((async context =>
        {
            context.ProxyResponse?.Headers.Add("x-Token-Gateway", "v1");
            await Task.CompletedTask;
        })));
    });

var app = builder.Build();

app.MapReverseProxy();

await app.RunAsync();