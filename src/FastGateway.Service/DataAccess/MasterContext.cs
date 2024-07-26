using System.Text.Json;
using FastGateway.Entities;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.DataAccess;

public sealed class MasterContext(DbContextOptions<MasterContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public DbSet<BlacklistAndWhitelist> BlacklistAndWhitelists { get; set; }

    public DbSet<DomainName> DomainNames { get; set; }

    public DbSet<Server> Servers { get; set; }

    public DbSet<Cert> Certs { get; set; }

    public DbSet<RateLimit> RateLimits { get; set; }

    public DbSet<Setting> Settings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigEntities(modelBuilder);
    }

    private void ConfigEntities(ModelBuilder builder)
    {
        builder.Entity<BlacklistAndWhitelist>(entity =>
        {
            entity.ToTable("blacklist_and_whitelist");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.Name).HasMaxLength(50);

            entity.Property(e => e.Description).HasMaxLength(200);

            entity.Property(e => e.Ips).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

            entity.HasIndex(e => e.Name);

            entity.HasIndex(e => e.Ips);

            entity.HasIndex(e => e.Enable);

            entity.HasIndex(e => e.IsBlacklist);

            entity.Property(e => e.Enable).HasDefaultValue(true);
        });

        builder.Entity<DomainName>(entity =>
        {
            entity.ToTable("domain_name");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.Domains).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));

            entity.Property(e => e.Headers).HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions),
                v => JsonSerializer.Deserialize<List<HeadersView>>(v, JsonSerializerOptions));

            entity.HasIndex(e => e.ServiceType);

            entity.HasIndex(e => e.Enable);

            entity.HasIndex(e => e.Service);

            entity.HasIndex(e => e.Root);

            entity.HasIndex(e => e.ServerId);

            entity.HasIndex(e => e.ServiceType);

            entity.Property(x => x.UpStreams).HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions),
                v => JsonSerializer.Deserialize<List<UpStream>>(v, JsonSerializerOptions));


            entity.Property(x => x.TryFiles).HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions),
                v => JsonSerializer.Deserialize<string[]>(v, JsonSerializerOptions));
        });

        builder.Entity<Server>(entity =>
        {
            entity.ToTable("server");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.Enable).HasDefaultValue(true);


            entity.HasIndex(e => e.Enable);
        });

        builder.Entity<Cert>(entity =>
        {
            entity.ToTable("cert");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(x => x.Certs).HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions),
                v => JsonSerializer.Deserialize<CertData>(v, JsonSerializerOptions));
        });

        builder.Entity<RateLimit>(entity =>
        {
            entity.ToTable("rate_limit");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.Enable).HasDefaultValue(true);

            entity.HasIndex(e => e.Enable);

            entity.Property(e => e.EndpointWhitelist).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));

            entity.Property(e => e.IpWhitelist).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));
        });

        builder.Entity<Setting>(options =>
        {
            options.ToTable("setting");

            options.HasKey(e => e.Key);

            options.Property(e => e.Key).HasMaxLength(50);

            options.Property(e => e.Value).HasMaxLength(200);

            options.Property(e => e.Description).HasMaxLength(200);

            options.Property(e => e.Group).HasMaxLength(50);

            options.HasIndex(e => e.IsPublic);

            options.HasIndex(e => e.IsSystem);
        });
    }
}