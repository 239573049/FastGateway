using StatisticRequestCount = FastGateway.Domain.StatisticRequestCount;

namespace FastGateway.EFCore;

public class MasterDbContext : DbContext
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options)
    {
    }

    public DbSet<Service?> Services { get; set; }

    public DbSet<Location> Locations { get; set; }

    public DbSet<Cert> Certs { get; set; }

    public DbSet<StatisticRequestCount> StatisticRequestCounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Service>(options =>
        {
            options.ToTable("service");

            options.HasKey(x => x.Id);

            options.Property(x => x.ServiceNames).HasConversion(
                v => string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries)
            );
        });

        modelBuilder.Entity<Location>(options =>
        {
            options.ToTable("location");

            options.HasKey(x => x.Id);

            options.HasIndex(x => x.ServiceId);

            options.Property(x => x.AddHeader).HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonSerializerOptions)
            );

            options.Property(x => x.TryFiles).HasConversion(
                v => string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries)
            );

            options.Property(x => x.UpStreams).HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions),
                v => JsonSerializer.Deserialize<List<UpStream>>(v, JsonSerializerOptions)
            );
        });

        modelBuilder.Entity<Cert>(options =>
        {
            options.ToTable("cert");

            options.HasKey(x => x.Id);

            options.Property(x => x.Domains).HasConversion(
                v => string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries)
            );

            options.Property(x => x.Certs).HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions),
                v => JsonSerializer.Deserialize<List<CertData>>(v, JsonSerializerOptions)
            );
        });

        modelBuilder.Entity<StatisticRequestCount>(options =>
        {
            options.ToTable("statistic_request_count");

            options.HasKey(x => x.Id);

            options.HasIndex(x => x.ServiceId);

            options.Property(x => x.Id)
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<StatisticIp>(options =>
        {
            options.ToTable("statistic_ip");

            // ip serviceIP聚合
            options.HasKey(x => new { x.Ip, x.ServiceId, x.Year, x.Month, x.Day });

            options.Property(x => x.Ip)
                .ValueGeneratedNever();
            
            options.Property(x => x.Id)
                .ValueGeneratedOnAdd();
            
            options.HasKey(x => x.Id);
        });
    }
}