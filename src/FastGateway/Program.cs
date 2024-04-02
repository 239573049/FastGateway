using FastGateway;
using FastGateway.BackgroundServices;
using FastGateway.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.GetSection("JwtOptions").Get<JwtOptions>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonDateTimeConverter());
});

builder.Services.AddSingleton<IQpsService,QpsService>();
builder.Services.AddHostedService<RenewSslBackgroundService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FastGateway", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddMapster();

builder.Services.AddDbContext<MasterDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("Default"));
});

builder.Services.AddService();

builder.Services
    .AddAuthorization()
    .AddJwtBearerAuthentication();

var app = builder.Build();

app.MapFastMiniApis();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/.well-known/acme-challenge/{token}", AcmeChallenge.Challenge);
app.MapPost("/.well-known/acme-challenge/{token}", AcmeChallenge.Challenge);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    await db.Database.MigrateAsync();

    await ApiServiceService.LoadServices(db);
}

app.Run();