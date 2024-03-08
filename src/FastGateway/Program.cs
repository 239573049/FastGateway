using System.Reflection;
using FastGateway.Middlewares.FlowAnalytics;

namespace FastGateway;

internal static class Program
{
    private static IFreeSql _freeSql;

    public static IFreeSql FreeSql => _freeSql;

    public static async Task Main(string[] args)
    {
        // 获取当前程序集版本
        var version = Assembly.GetExecutingAssembly().GetName().Version;

        var title = """
                    
                               ______________
                           ,===:'.,            `-._
                                `:.`---.__         `-._
                                  `:.     `--.         `.
                                    \.        `.         `.
                            (,,(,    \.         `.   ____,-`.,
                         (,'     `/   \.   ,--.___`.'
                     ,  ,'  ,--.  `,   \.;'         `
                      `{D, {    \  :    \;
                        V,,'    /  /    //
                        j;;    /  ,' ,-//.    ,---.      ,
                        \;'   /  ,' /  _  \  /  _  \   ,'/
                              \   `'  / \  `'  / \  `.' /
                               `.___,'   `.__,'   `.__,'
                    神龙保佑,代码无BUG!
                    [欢迎使用Token FastGateway]
                    [版本：v{version}]
                    [作者：token]

                    """;
        title = title.Replace("{version}", version?.ToString() ?? "1.0.0");

        // 给控制台输出一个彩色的标题
        Console.ForegroundColor = ConsoleColor.Green;

        Console.WriteLine(title);

        Console.ResetColor();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        #region FreeSql类型转换

        Utils.TypeHandlers.TryAdd(typeof(Dictionary<string, string>),
            new StringJsonHandler<Dictionary<string, string>>());
        Utils.TypeHandlers.TryAdd(typeof(List<DestinationsEntity>), new StringJsonHandler<List<DestinationsEntity>>());
        Utils.TypeHandlers.TryAdd(typeof(string[]), new StringJsonHandler<string[]>());

        #endregion


        // var inMemoryConfigProvider = serviceProvider.GetRequiredService<InMemoryConfigProvider>();
        // var requestSourceService = serviceProvider.GetRequiredService<RequestSourceService>();

        var builder = WebApplication.CreateBuilder(args);

        FreeSqlContext.Initialize(new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, builder.Configuration.GetConnectionString("DefaultConnection"))
            .UseMonitorCommand(cmd => Console.WriteLine($"Sql：{cmd.CommandText}")) //监听SQL语句
            .UseAutoSyncStructure(true) //自动同步实体结构到数据库，FreeSql不会扫描程序集，只有CRUD时才会生成表。
            .Build());

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                policyBuilder => policyBuilder
                    .SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
        });


        builder.Configuration.GetSection(nameof(JwtOptions))
            .Get<JwtOptions>();

        builder.Configuration.GetSection(GatewayOptions.Name)
            .Get<GatewayOptions>();

        builder.Services.AddSingleton<GatewayService>();
        builder.Services.AddSingleton<CertificateService>();
        builder.Services.AddSingleton<StaticFileProxyService>();
        builder.Services.AddSingleton<IFlowAnalyzer, FlowAnalyzer>();
        builder.Services.AddSingleton<RequestSourceService>();

        builder.Services.AddHostedService<GatewayBackgroundService>();

        builder.Services
            .AddAuthorization()
            .AddJwtBearerAuthentication();

        var app = builder.Build();

        app.UseCors("AllowAll");

        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/")
            {
                context.Request.Path = "/index.html";
            }

            await next(context);

            if (context.Response.StatusCode == 404)
            {
                context.Request.Path = "/index.html";
                await next(context);
            }
        });

        // 配置MiniApis服务
        app.MapStaticFileProxy();
        app.MapFileStorage();
        app.MapGateway();
        app.MapAuthority();
        app.MapCertificate();
        app.MapSystem();
        app.MapRequestSource();
        app.MapPortManagement();

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        await app.RunAsync("http://*:8000");
    }
}