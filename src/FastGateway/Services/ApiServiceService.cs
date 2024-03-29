using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace FastGateway.Services;

public class ApiServiceService() : ServiceBase("/api/v1/ApiService")
{
    private readonly Dictionary<string, WebApplication> _webApplications = new();

    public async Task LoadServices(MasterDbContext masterDbContext)
    {
        foreach (var application in _webApplications)
        {
            await application.Value.DisposeAsync();
        }

        _webApplications.Clear();

        var services = await masterDbContext.Services
            .AsNoTracking()
            .Include(x => x.Locations)
            .ToListAsync();

        foreach (var service in services)
        {
            await Task.Factory.StartNew(BuilderService, service);
        }
    }

    public async Task CreateAsync(ServiceInput input, MasterDbContext masterDbContext)
    {
        var service = input.Adapt<Service>();

        service.Id = Guid.NewGuid().ToString();

        service.Locations.ForEach(x =>
        {
            x.Id = Guid.NewGuid().ToString();

            x.ServiceId = service.Id;
        });

        await masterDbContext.Services.AddAsync(service);

        await masterDbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(string id, ServiceInput input, MasterDbContext masterDbContext)
    {
        var service = await masterDbContext.Services.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (service == null)
        {
            throw new Exception("Service not found");
        }

        input.Adapt(service);

        masterDbContext.Update(service);

        await masterDbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id, MasterDbContext masterDbContext)
    {
        try
        {
            await masterDbContext.Database.BeginTransactionAsync();

            await masterDbContext.Services.Where(x => x.Id == id)
                .ExecuteDeleteAsync();

            await masterDbContext.Locations.Where(x => x.ServiceId == id)
                .ExecuteDeleteAsync();

            await masterDbContext.Database.CommitTransactionAsync();
        }
        catch (Exception e)
        {
            await masterDbContext.Database.RollbackTransactionAsync();

            throw e;
        }
    }

    public async Task<Service> GetAsync(string id, MasterDbContext masterDbContext)
    {
        return await masterDbContext.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<Service>> GetAllAsync(MasterDbContext masterDbContext)
    {
        await LoadServices(masterDbContext);
        return await masterDbContext.Services
            .AsNoTracking()
            .Include(x => x.Locations)
            .ToListAsync();
    }


    public async Task BuilderService(object state)
    {
        var service = (Service)state;

        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseKestrel(options =>
        {
            options.Listen(IPAddress.Parse("127.0.0.1"), service.Listen, listenOptions =>
            {
                if (service.IsHttps)
                {
                    listenOptions.UseHttps(service.SslCertificate, service.SslCertificatePassword);
                }

                if (service.EnableHttp3)
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                }

                listenOptions.UseConnectionLogging();
            });
        });

        builder.Services.AddReverseProxy();

        var app = builder.Build();

        _webApplications.Add(service.Id, app);

        foreach (var location in service.Locations)
        {
            if (!string.IsNullOrWhiteSpace(location.ProxyPass))
            {
                if (location.UpStreams.Count == 0)
                {
                    app.MapForwarder(location.Path + "{**catch-all}", location.ProxyPass);
                }
                else
                {
                    app.MapForwarder(location.Path,
                        location.UpStreams.OrderBy(x => Guid.NewGuid()).Select(x => x.Server).First());
                }
            }
            else if (!string.IsNullOrWhiteSpace(location.Root))
            {
                app.MapGet(location.Path, async context =>
                {
                    var path = Path.Combine(location.Root, context.Request.Path.Value[1..]);

                    if (File.Exists(path))
                    {
                        await context.Response.SendFileAsync(path);
                    }

                    // 搜索 try_files
                    foreach (var tryFile in location.TryFiles)
                    {
                        var tryPath = Path.Combine(location.Root, tryFile);

                        if (File.Exists(tryPath))
                        {
                            await context.Response.SendFileAsync(tryPath);
                        }
                    }
                });
            }
        }

        await app.RunAsync();
    }
}