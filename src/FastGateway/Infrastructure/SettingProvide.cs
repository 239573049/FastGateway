using Core.Entities;
using FastGateway.Services;

namespace FastGateway.Infrastructure;

public class SettingProvide(ConfigurationService configService)
{
    public ValueTask<int> GetIntAsync(string key)
    {
        var setting = configService.GetSettings().FirstOrDefault(x => x.Key == key);
        if (setting?.Value != null && int.TryParse(setting.Value, out var result)) return ValueTask.FromResult(result);

        return ValueTask.FromResult(0);
    }

    public ValueTask<string> GetStringAsync(string key)
    {
        var setting = configService.GetSettings().FirstOrDefault(x => x.Key == key);
        return ValueTask.FromResult(setting?.Value);
    }

    public ValueTask<bool> GetBoolAsync(string key)
    {
        var setting = configService.GetSettings().FirstOrDefault(x => x.Key == key);
        if (setting?.Value != null && bool.TryParse(setting.Value, out var result)) return ValueTask.FromResult(result);

        return ValueTask.FromResult(false);
    }

    public ValueTask<T> GetEnumAsync<T>(string key) where T : struct
    {
        var setting = configService.GetSettings().FirstOrDefault(x => x.Key == key);
        if (setting?.Value != null && Enum.TryParse<T>(setting.Value, out var result))
            return ValueTask.FromResult(result);

        return ValueTask.FromResult(default(T));
    }

    public ValueTask<List<Setting>> GetListAsync(string group)
    {
        var result = configService.GetSettings().Where(x => x.Group == group).ToList();
        return ValueTask.FromResult(result);
    }

    public ValueTask<List<Setting>> GetListAsync()
    {
        var result = configService.GetSettings().ToList();
        return ValueTask.FromResult(result);
    }

    public ValueTask SetAsync(string key, Setting setting)
    {
        setting.Key = key;
        configService.AddOrUpdateSetting(setting);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetAsync(string key, string value)
    {
        return SetAsync(key, new Setting
        {
            Value = value,
            Description = string.Empty,
            IsPublic = false,
            IsSystem = false
        });
    }
}