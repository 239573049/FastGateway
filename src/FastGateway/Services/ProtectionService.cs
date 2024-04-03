namespace FastGateway.Services;

public static class ProtectionService
{
    private static List<BlacklistAndWhitelist> _blacklistAndWhitelists = new(1);

    public static async Task LoadBlacklistAndWhitelistAsync(MasterDbContext masterDbContext)
    {
        _blacklistAndWhitelists = await masterDbContext
            .BlacklistAndWhitelists
            .Where(x => x.Enable)
            .ToListAsync();
    }

    public static bool CheckBlacklistAndWhitelist(string ip, ProtectionType type)
    {
        // 如果存在白名单 则只允许白名单
        if (type == ProtectionType.Whitelist)
        {
            // ip可能是ip端，也可能是ip范围，判断是否在范围内，如果在范围内则返回true
            return _blacklistAndWhitelists.Where(x => x.Type == ProtectionType.Whitelist).Any(x =>
            {
                return x.Ips.Any(x => IpHelper.UnsafeCheckIpInIpRange(ip, x));
            });
        }

        return _blacklistAndWhitelists.Where(x => x.Type == ProtectionType.Blacklist).Any(x =>
        {
            return x.Ips.Any(ipRange => IpHelper.UnsafeCheckIpInIpRange(ip, ipRange));
        });
    }

    public static async Task<ResultDto> CreateBlacklistAndWhitelistAsync(MasterDbContext masterDbContext,
        BlacklistAndWhitelist blacklist)
    {
        // 校验ips格式是否符合 ip范围 ip端 但个ip的格式
        foreach (var ip in blacklist.Ips)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return ResultDto.ErrorResult("ip不能为空");
            }

            if (!ip.Contains('-') && !ip.Contains('/') && !ip.Contains('.'))
            {
                return ResultDto.ErrorResult("ip格式不正确");
            }

            // 校验ip范围格式
            if (ip.Contains('/'))
            {
                var ipRanges = ip.Split('/');
                if (ipRanges.Length != 2)
                {
                    return ResultDto.ErrorResult("ip范围格式不正确");
                }

                var startIp = ipRanges[0];
                var mask = ipRanges[1];

                var startIpArray = startIp.Split('.');
                if (startIpArray.Length != 4)
                {
                    return ResultDto.ErrorResult("ip范围格式不正确");
                }

                if (!int.TryParse(mask, out _))
                {
                    return ResultDto.ErrorResult("ip范围格式不正确");
                }

                for (var i = 0; i < 4; i++)
                {
                    if (!int.TryParse(startIpArray[i], out _))
                    {
                        return ResultDto.ErrorResult("ip范围格式不正确");
                    }
                }
            }
            else if (ip.Contains('-'))
            {
                var ipRanges = ip.Split('-');
                if (ipRanges.Length != 2)
                {
                    return ResultDto.ErrorResult("ip范围格式不正确");
                }

                var startIp = ipRanges[0];
                var endIp = ipRanges[1];

                var startIpArray = startIp.Split('.');
                var endIpArray = endIp.Split('.');

                if (startIpArray.Length != 4 || endIpArray.Length != 4)
                {
                    return ResultDto.ErrorResult("ip范围格式不正确");
                }

                for (var i = 0; i < 4; i++)
                {
                    if (!int.TryParse(startIpArray[i], out _) || !int.TryParse(endIpArray[i], out _))
                    {
                        return ResultDto.ErrorResult("ip范围格式不正确");
                    }
                }
            }
        }

        var blacklistEntity = new BlacklistAndWhitelist()
        {
            Name = blacklist.Name,
            Description = blacklist.Description,
            Ips = blacklist.Ips,
            Enable = blacklist.Enable,
            Type = blacklist.Type
        };

        await masterDbContext.BlacklistAndWhitelists.AddAsync(blacklistEntity);

        await masterDbContext.SaveChangesAsync();

        return ResultDto.SuccessResult();
    }

    public static async Task<ResultDto> UpdateBlacklistAsync(MasterDbContext masterDbContext,
        BlacklistAndWhitelist blacklist)
    {
        // 校验ips格式是否符合 ip范围 ip端 但个ip的格式
        foreach (var ip in blacklist.Ips)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return ResultDto.ErrorResult("ip不能为空");
            }

            if (!ip.Contains('-') && !ip.Contains('/') && !ip.Contains('.'))
            {
                return ResultDto.ErrorResult("ip格式不正确");
            }

            // 校验ip范围格式
            if (ip.Contains('/'))
            {
                var ipRanges = ip.Split('/');
                if (ipRanges.Length != 2)
                {
                    return ResultDto.ErrorResult("ip范围格式不正确");
                }

                var startIp = ipRanges[0];
                var mask = ipRanges[1];

                var startIpArray = startIp.Split('.');
                if (startIpArray.Length != 4)
                {
                    return ResultDto.ErrorResult("ip范围格式不正确");
                }

                if (!int.TryParse(mask, out _))
                {
                    return ResultDto.ErrorResult("ip范围格式不正确");
                }

                for (var i = 0; i < 4; i++)
                {
                    if (!int.TryParse(startIpArray[i], out _))
                    {
                        return ResultDto.ErrorResult("ip范围格式不正确");
                    }
                }
            }
            else if (ip.Contains('-'))
            {
                var ipRanges = ip.Split('-');
                if (ipRanges.Length != 2)
                {
                    return ResultDto.ErrorResult("ip范围格式不正确");
                }

                var startIp = ipRanges[0];
                var endIp = ipRanges[1];

                var startIpArray = startIp.Split('.');
                var endIpArray = endIp.Split('.');

                if (startIpArray.Length != 4 || endIpArray.Length != 4)
                {
                    return ResultDto.ErrorResult("ip范围格式不正确");
                }

                for (var i = 0; i < 4; i++)
                {
                    if (!int.TryParse(startIpArray[i], out _) || !int.TryParse(endIpArray[i], out _))
                    {
                        return ResultDto.ErrorResult("ip范围格式不正确");
                    }
                }
            }
        }

        await masterDbContext.BlacklistAndWhitelists
            .Where(x => x.Id == blacklist.Id)
            .ExecuteUpdateAsync(x =>
                x.SetProperty(i => i.Name, blacklist.Name)
                    .SetProperty(i => i.Description, blacklist.Description)
                    .SetProperty(i => i.Ips, blacklist.Ips)
                    .SetProperty(i => i.Enable, blacklist.Enable)
                    .SetProperty(i => i.Type, blacklist.Type));


        return ResultDto.SuccessResult();
    }

    public static async Task DeleteBlacklistAsync(MasterDbContext masterDbContext, long id)
    {
        await masterDbContext.BlacklistAndWhitelists.Where(x => x.Id == id)
            .ExecuteDeleteAsync();

        await masterDbContext.SaveChangesAsync();
    }

    public static async Task<ResultDto<PageResultDto<BlacklistAndWhitelist>>> GetBlacklistListAsync(
        MasterDbContext masterDbContext,
        int page, int pageSize)
    {
        var items = await masterDbContext.BlacklistAndWhitelists
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var total = await masterDbContext.BlacklistAndWhitelists.CountAsync();

        return ResultDto<PageResultDto<BlacklistAndWhitelist>>.SuccessResult(
            new PageResultDto<BlacklistAndWhitelist>(items, total));
    }

    public static async Task EnableBlacklistAndWhitelistAsync(MasterDbContext masterDbContext, long id, bool enable)
    {
        await masterDbContext.BlacklistAndWhitelists.Where(x => x.Id == id)
            .ExecuteUpdateAsync(x =>
                x.SetProperty(i => i.Enable, enable));
    }
}