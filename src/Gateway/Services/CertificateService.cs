namespace Gateway.Services;

public class CertificateService(IFreeSql freeSql)
{
    public static ConcurrentDictionary<string, CertificateEntity> CertificateEntityDict { get; private set; } = new();

    public async Task RefreshConfig()
    {
        var certificateEntities = await freeSql.Select<CertificateEntity>().ToListAsync();

        foreach (var entity in certificateEntities)
        {
            CertificateEntityDict.TryAdd(entity.Host, entity);
        }
    }

    public async Task<ResultDto> CreateAsync(CertificateEntity certificateEntity)
    {
        certificateEntity.Id = Guid.NewGuid().ToString();
        certificateEntity.CreatedTime = DateTime.Now;


        if (await freeSql.Select<CertificateEntity>().AnyAsync(x => x.Host == certificateEntity.Host))
        {
            return ResultDto.Error("域名已存在");
        }

        await freeSql.Insert(certificateEntity).ExecuteAffrowsAsync();

        CertificateEntityDict.TryAdd(certificateEntity.Host, certificateEntity);

        return ResultDto.Success();
    }

    public async Task<ResultDto> UpdateAsync(CertificateEntity certificateEntity)
    {
        var entity = await freeSql.Select<CertificateEntity>().Where(x => x.Id == certificateEntity.Id).FirstAsync();

        if (entity == null)
        {
            return ResultDto.Error("证书不存在");
        }

        entity.Name = certificateEntity.Name;
        entity.Description = certificateEntity.Description;
        entity.Host = certificateEntity.Host;
        entity.Password = certificateEntity.Password;
        entity.UpdateTime = DateTime.Now;

        await freeSql.Update<CertificateEntity>().SetSource(entity).ExecuteAffrowsAsync();

        CertificateEntityDict[certificateEntity.Host] = certificateEntity;

        return ResultDto.Success();
    }

    public async Task<ResultDto> DeleteAsync(string id)
    {
        var certificateEntity = await freeSql.Select<CertificateEntity>().Where(x => x.Id == id).FirstAsync();

        if (certificateEntity == null)
        {
            return ResultDto.Error("证书不存在");
        }

        await freeSql.Delete<CertificateEntity>().Where(x => x.Id == id).ExecuteAffrowsAsync();

        CertificateEntityDict.TryRemove(certificateEntity.Host, out _);

        return ResultDto.Success();
    }

    public async Task<ResultDto> GetAsync()
    {
        var certificateEntities = await freeSql.Select<CertificateEntity>().ToListAsync();

        return ResultDto.Success(certificateEntities);
    }

    public async Task UpdateCertificateAsync(string id, string path)
    {
        var certificateEntity = await freeSql.Select<CertificateEntity>().Where(x => x.Id == id).FirstAsync();

        if (certificateEntity == null)
        {
            return;
        }

        certificateEntity.Path = path;
        certificateEntity.UpdateTime = DateTime.Now;

        await freeSql.Update<CertificateEntity>().SetSource(certificateEntity).ExecuteAffrowsAsync();

        CertificateEntityDict[certificateEntity.Host] = certificateEntity;
    }
}

public static class CertificateExtension
{
    public static void MapCertificate(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gateway/certificates", async (CertificateService certificateService) =>
                await certificateService.GetAsync())
            .RequireAuthorization();

        app.MapPost("/api/gateway/certificates",
                async (CertificateService certificateService, CertificateEntity certificateEntity) =>
                    await certificateService.CreateAsync(certificateEntity))
            .RequireAuthorization();

        app.MapPut("/api/gateway/certificates",
                async (CertificateService certificateService, CertificateEntity certificateEntity) =>
                    await certificateService.UpdateAsync(certificateEntity))
            .RequireAuthorization();

        app.MapDelete("/api/gateway/certificates/{id}", async (CertificateService certificateService, string id) =>
                await certificateService.DeleteAsync(id))
            .RequireAuthorization();

        app.MapPut("/api/gateway/certificates/{id}",
                async (string id, string path, CertificateService certificateService) =>
                    await certificateService.UpdateCertificateAsync(id, path))
            .RequireAuthorization();
    }
}