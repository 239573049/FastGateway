using FastGateway.Entities;
using FastGateway.Service.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.Infrastructure;

public class SettingProvide(MasterContext context) : IScopeDependency
{
    public async ValueTask<int> GetIntAsync(string key)
    {
        var setting = await context.Settings.FirstOrDefaultAsync(x => x.Key == key);
        if (setting?.Value != null && int.TryParse(setting.Value, out var result))
        {
            return result;
        }

        return 0;
    }

    public async ValueTask<string> GetStringAsync(string key)
    {
        var setting = await context.Settings.FirstOrDefaultAsync(x => x.Key == key);
        return setting?.Value;
    }

    public async ValueTask<bool> GetBoolAsync(string key)
    {
        var setting = await context.Settings.FirstOrDefaultAsync(x => x.Key == key);
        if (setting?.Value != null && bool.TryParse(setting.Value, out var result))
        {
            return result;
        }

        return false;
    }

    public async ValueTask<T> GetEnumAsync<T>(string key) where T : struct
    {
        var setting = await context.Settings.FirstOrDefaultAsync(x => x.Key == key);
        if (setting?.Value != null && Enum.TryParse<T>(setting.Value, out var result))
        {
            return result;
        }

        return default;
    }

    public async ValueTask<List<Setting>> GetListAsync(string group)
    {
        return await context.Settings.Where(x => x.Group == group).ToListAsync();
    }

    public async ValueTask<List<Setting>> GetListAsync()
    {
        return await context.Settings.ToListAsync();
    }

    public async ValueTask SetAsync(string key, Setting setting)
    {
        setting.Key = key;

        if (context.Settings.Any(x => x.Key == key))
        {
            await context.Settings.Where(x => x.Key == key)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.Value, a => setting.Value));
        }
        else
        {
            await context.Settings.AddAsync(setting);

            await context.SaveChangesAsync();
        }
    }

    public async ValueTask SetAsync(string key, string value)
    {
        await SetAsync(key, new Setting
        {
            Value = value,
            Description = string.Empty,
            IsPublic = false,
            IsSystem = false
        }).ConfigureAwait(false);
    }
}