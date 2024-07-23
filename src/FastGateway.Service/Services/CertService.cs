using System.ComponentModel.DataAnnotations;
using FastGateway.Entities;
using FastGateway.Service.DataAccess;
using FastGateway.Service.Dto;
using FastGateway.Service.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.Services;

public static class CertService
{
    public static WebApplication MapCert(this WebApplication app)
    {
        var cert = app.MapGroup("/api/v1/cert")
            .WithTags("证书")
            .WithDescription("证书管理")
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("证书");

        cert.MapPost(string.Empty, async (MasterContext dbContext, Cert cert) =>
        {
            if (string.IsNullOrWhiteSpace(cert.Domain))
            {
                throw new ValidationException("域名不能为空");
            }

            if (await dbContext.Certs.AnyAsync(x => x.Domain == cert.Domain))
            {
                throw new ValidationException("域名已存在");
            }

            cert.CreateTime = DateTime.Now;
            dbContext.Certs.Add(cert);

            await dbContext.SaveChangesAsync();
        }).WithDescription("创建证书").WithDisplayName("创建证书").WithTags("证书");

        cert.MapGet(string.Empty, async (MasterContext dbContext, int page, int pageSize) =>
            {
                var result = await dbContext.Certs
                    .OrderByDescending(x => x.CreateTime)
                    .Skip((page - 1) * pageSize)
                    .ToListAsync();

                var total = await dbContext.Certs.CountAsync();

                return new PagingDto<Cert>(total, result);
            })
            .WithDescription("获取证书列表")
            .WithDisplayName("获取证书列表")
            .WithTags("证书");

        cert.MapDelete("{id}",
            async (MasterContext dbContext, string id) =>
            {
                await dbContext.Certs.Where(x => x.Id == id).ExecuteDeleteAsync();
            }).WithDescription("删除证书").WithDisplayName("删除证书").WithTags("证书");

        cert.MapPut("{id}", async (MasterContext dbContext, string id, Cert cert) =>
        {
            if (string.IsNullOrWhiteSpace(cert.Domain))
            {
                throw new ValidationException("域名不能为空");
            }

            if (await dbContext.Certs.AnyAsync(x => x.Domain == cert.Domain && x.Id != id))
            {
                throw new ValidationException("域名已存在");
            }

            cert.Id = id;
            dbContext.Certs.Update(cert);

            await dbContext.SaveChangesAsync();
        }).WithDescription("更新证书").WithDisplayName("更新证书").WithTags("证书");


        return app;
    }
}