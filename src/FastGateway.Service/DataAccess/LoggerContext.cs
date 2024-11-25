using System.Text.Json;
using FastGateway.Entities;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.DataAccess;

public class LoggerContext(DbContextOptions<LoggerContext> options) : DbContext(options)
{
    
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    
    public DbSet<ApplicationLogger> ApplicationLoggers { get; set; }
    
    public DbSet<ClientRequestLogger> ClientRequestLoggers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.LogTo(_ => {});
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<ApplicationLogger>(entity =>
        {
            entity.ToTable("application_logger");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(x => x.Extend).HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonSerializerOptions));
        });

        modelBuilder.Entity<ClientRequestLogger>(options =>
        {
            options.ToTable("client_request_logger");

            options.HasKey(e => e.Id);

            options.Property(e => e.Id).ValueGeneratedOnAdd();

            options.Property(e => e.Ip).HasMaxLength(50);
        });
    }
}