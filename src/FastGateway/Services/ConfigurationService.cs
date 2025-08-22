using System.Text.Json;
using Core.Entities;

namespace FastGateway.Services;

/// <summary>
///     配置文件管理服务，替代EntityFrameworkCore
/// </summary>
public class ConfigurationService
{
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Lock _lockObject = new();
    private GatewayConfig _config;

    public ConfigurationService()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, "data", "gateway.config");

        // 判断目录是否存在，如果不存在则创建
        var directory = Path.GetDirectoryName(_configPath);
        if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        LoadConfig();
    }

    private void LoadConfig()
    {
        lock (_lockObject)
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    _config = JsonSerializer.Deserialize<GatewayConfig>(json, _jsonOptions) ?? new GatewayConfig();
                }
                catch
                {
                    _config = new GatewayConfig();
                }
            }
            else
            {
                _config = new GatewayConfig();
                SaveConfig();
            }
        }
    }

    private void SaveConfig()
    {
        lock (_lockObject)
        {
            var json = JsonSerializer.Serialize(_config, _jsonOptions);
            File.WriteAllText(_configPath, json);
        }
    }

    // Server operations
    public List<Server> GetServers()
    {
        return _config.Servers.ToList();
    }

    public Server? GetServer(string id)
    {
        return _config.Servers.FirstOrDefault(s => s.Id == id);
    }

    public void AddServer(Server server)
    {
        if (string.IsNullOrEmpty(server.Id))
            server.Id = Guid.NewGuid().ToString();

        _config.Servers.Add(server);
        SaveConfig();
    }

    public void UpdateServer(Server server)
    {
        var index = _config.Servers.FindIndex(s => s.Id == server.Id);
        if (index >= 0)
        {
            _config.Servers[index] = server;
            SaveConfig();
        }
    }

    public void DeleteServer(string id)
    {
        _config.Servers.RemoveAll(s => s.Id == id);
        SaveConfig();
    }

    // DomainName operations
    public List<DomainName> GetDomainNames()
    {
        return _config.DomainNames.ToList();
    }

    public DomainName[] GetDomainNamesByServerId(string serverId)
    {
        return _config.DomainNames.Where(d => d.ServerId == serverId).ToArray();
    }

    public void AddDomainName(DomainName domainName)
    {
        if (string.IsNullOrEmpty(domainName.Id))
            domainName.Id = Guid.NewGuid().ToString();

        _config.DomainNames.Add(domainName);
        SaveConfig();
    }

    public void UpdateDomainName(DomainName domainName)
    {
        var index = _config.DomainNames.FindIndex(d => d.Id == domainName.Id);
        if (index >= 0)
        {
            _config.DomainNames[index] = domainName;
            SaveConfig();
        }
    }

    public void DeleteDomainName(string id)
    {
        _config.DomainNames.RemoveAll(d => d.Id == id);
        SaveConfig();
    }

    // Cert operations
    public List<Cert> GetCerts()
    {
        return _config.Certs.ToList();
    }

    public Cert[] GetActiveCerts()
    {
        return _config.Certs.Where(c => !c.Expired).ToArray();
    }

    public void AddCert(Cert cert)
    {
        if (string.IsNullOrEmpty(cert.Id))
            cert.Id = Guid.NewGuid().ToString();

        _config.Certs.Add(cert);
        SaveConfig();
    }

    public void UpdateCert(Cert cert)
    {
        var index = _config.Certs.FindIndex(c => c.Id == cert.Id);
        if (index >= 0)
        {
            _config.Certs[index] = cert;
            SaveConfig();
        }
    }

    public void DeleteCert(string id)
    {
        _config.Certs.RemoveAll(c => c.Id == id);
        SaveConfig();
    }

    // BlacklistAndWhitelist operations
    public List<BlacklistAndWhitelist> GetBlacklistAndWhitelists()
    {
        return _config.BlacklistAndWhitelists.ToList();
    }

    public void AddBlacklistAndWhitelist(BlacklistAndWhitelist item)
    {
        if (item.Id == 0)
            item.Id = _config.BlacklistAndWhitelists.Count > 0 ? _config.BlacklistAndWhitelists.Max(b => b.Id) + 1 : 1;

        _config.BlacklistAndWhitelists.Add(item);
        SaveConfig();
    }

    public void UpdateBlacklistAndWhitelist(BlacklistAndWhitelist item)
    {
        var index = _config.BlacklistAndWhitelists.FindIndex(b => b.Id == item.Id);
        if (index >= 0)
        {
            _config.BlacklistAndWhitelists[index] = item;
            SaveConfig();
        }
    }

    public void DeleteBlacklistAndWhitelist(long id)
    {
        _config.BlacklistAndWhitelists.RemoveAll(b => b.Id == id);
        SaveConfig();
    }

    // RateLimit operations
    public List<RateLimit> GetRateLimits()
    {
        return _config.RateLimits.ToList();
    }

    public void AddRateLimit(RateLimit rateLimit)
    {
        if (string.IsNullOrEmpty(rateLimit.Id))
            rateLimit.Id = Guid.NewGuid().ToString();

        _config.RateLimits.Add(rateLimit);
        SaveConfig();
    }

    public void UpdateRateLimit(RateLimit rateLimit)
    {
        var index = _config.RateLimits.FindIndex(r => r.Id == rateLimit.Id);
        if (index >= 0)
        {
            _config.RateLimits[index] = rateLimit;
            SaveConfig();
        }
    }

    public void DeleteRateLimit(string id)
    {
        _config.RateLimits.RemoveAll(r => r.Id == id);
        SaveConfig();
    }

    // Setting operations
    public List<Setting> GetSettings()
    {
        return _config.Settings.ToList();
    }

    public Setting? GetSetting(string key)
    {
        return _config.Settings.FirstOrDefault(s => s.Key == key);
    }

    public void AddSetting(Setting setting)
    {
        _config.Settings.Add(setting);
        SaveConfig();
    }

    public void UpdateSetting(Setting setting)
    {
        var index = _config.Settings.FindIndex(s => s.Key == setting.Key);
        if (index >= 0)
        {
            _config.Settings[index] = setting;
            SaveConfig();
        }
    }

    public void AddOrUpdateSetting(Setting setting)
    {
        var index = _config.Settings.FindIndex(s => s.Key == setting.Key);
        if (index >= 0)
            _config.Settings[index] = setting;
        else
            _config.Settings.Add(setting);

        SaveConfig();
    }

    public void DeleteSetting(string key)
    {
        _config.Settings.RemoveAll(s => s.Key == key);
        SaveConfig();
    }
}

/// <summary>
///     网关配置数据结构
/// </summary>
public class GatewayConfig
{
    public List<Server> Servers { get; set; } = new();
    public List<DomainName> DomainNames { get; set; } = new();
    public List<Cert> Certs { get; set; } = new();
    public List<BlacklistAndWhitelist> BlacklistAndWhitelists { get; set; } = new();
    public List<RateLimit> RateLimits { get; set; } = new();
    public List<Setting> Settings { get; set; } = new();
}