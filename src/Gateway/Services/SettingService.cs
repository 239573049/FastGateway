namespace Gateway.Services;

/// <summary>
/// 系统设置服务
/// </summary>
/// <param name="freeSql"></param>
public class SettingService(IFreeSql freeSql)
{
    public async Task<string?> GetAsync(string name)
    {
        var setting = await freeSql.Select<SettingEntity>().Where(x => x.Name == name).FirstAsync();

        return setting?.DefaultValue;
    }

    public async Task<T?> GetAsync<T>(string name)
    {
        var setting = await freeSql.Select<SettingEntity>().Where(x => x.Name == name).FirstAsync();

        try
        {
            // 如果是string则直接返回
            if (typeof(T) == typeof(string))
            {
                return (T)Convert.ChangeType(setting?.DefaultValue, typeof(T));
            }

            // 如果是值类型则直接返回
            if (typeof(T).IsValueType)
            {
                return (T)Convert.ChangeType(setting?.DefaultValue, typeof(T));
            }

            // 如果是引用类型则反序列化
            return JsonSerializer.Deserialize<T>(setting?.DefaultValue);
        }
        catch
        {
            return default;
        }
    }

    public async Task DeleteAsync(string name)
    {
        await freeSql.Delete<SettingEntity>().Where(x => x.Name == name).ExecuteAffrowsAsync();
    }

    public async Task<string> GetValueAsync(string name, string key)
    {
        var setting = await freeSql.Select<SettingEntity>().Where(x => x.Name == name).FirstAsync();

        if (setting.ExtraProperties.TryGetValue(key, out var value))
        {
            return value;
        }

        return string.Empty;
    }

    public async Task<T?> GetValueAsync<T>(string name, string key)
    {
        var setting = await freeSql.Select<SettingEntity>().Where(x => x.Name == name).FirstAsync();

        if (setting.ExtraProperties.TryGetValue(key, out var value))
        {
            try
            {
                // 如果是string则直接返回
                if (typeof(T) == typeof(string))
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }

                // 如果是值类型则直接返回
                if (typeof(T).IsValueType)
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }

                // 如果是引用类型则反序列化
                return JsonSerializer.Deserialize<T>(value);
            }
            catch
            {
                return default;
            }
        }

        return default;
    }

    public async Task<bool> AnyAsync(string name)
    {
        return await freeSql.Select<SettingEntity>().AnyAsync(x => x.Name == name);
    }

    public async Task SetAsync(string name, string value, string? displayName = null, string? description = null)
    {
        var setting = await freeSql.Select<SettingEntity>().Where(x => x.Name == name).FirstAsync();

        if (setting == null)
        {
            setting = new SettingEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                DefaultValue = value,
                DisplayName = displayName,
                Description = description,
                CreatedTime = DateTime.Now
            };
            await freeSql.Insert(setting).ExecuteAffrowsAsync();
        }
        else
        {
            setting.DefaultValue = value;
            await freeSql.Update<SettingEntity>().SetSource(setting).ExecuteAffrowsAsync();
        }
    }

    public async Task SetAsync<T>(string name, T value, string? displayName = null, string? description = null)
    {
        var setting = await freeSql.Select<SettingEntity>().Where(x => x.Name == name).FirstAsync();

        string defaultValue;

        try
        {
            // 如果是string则直接返回
            if (typeof(T) == typeof(string))
            {
                defaultValue = (string)Convert.ChangeType(value, typeof(string));
            }
            // 如果是值类型则直接返回
            else if (typeof(T).IsValueType)
            {
                defaultValue = value.ToString();
            }
            // 如果是引用类型则反序列化
            else
            {
                defaultValue = JsonSerializer.Serialize(value);
            }
        }
        catch
        {
            defaultValue = string.Empty;
        }

        if (setting == null)
        {
            setting = new SettingEntity
            {
                Name = name,
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                Description = description,
                DefaultValue = defaultValue
            };
            await freeSql.Insert(setting).ExecuteAffrowsAsync();
        }
        else
        {
            setting.DefaultValue = defaultValue;
            await freeSql.Update<SettingEntity>().SetSource(setting).ExecuteAffrowsAsync();
        }
    }

    public async Task<List<SettingEntity>> GetListAsync()
    {
        return await freeSql.Select<SettingEntity>()
            .OrderByDescending(x => x.CreatedTime)
            .ToListAsync();
    }
}

public static class SettingExtension
{
    public static void MapSetting(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gateway/setting", async (SettingService settingService) =>
            await settingService.GetListAsync());

        app.MapPut("/api/gateway/setting/{name}", async (SettingService settingService, string name, string value) =>
            await settingService.SetAsync(name, value));
    }
}