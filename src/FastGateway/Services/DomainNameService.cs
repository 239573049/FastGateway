using System.ComponentModel.DataAnnotations;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;

namespace FastGateway.Services;

public static class DomainNameService
{
    public static IEndpointRouteBuilder MapDomain(this IEndpointRouteBuilder app)
    {
        var domain = app.MapGroup("/api/v1/domain")
            .WithTags("域名")
            .WithDescription("域名管理")
            .RequireAuthorization()
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("域名");

        domain.MapPost(string.Empty, (ConfigurationService configService, DomainName domainName) =>
        {
            if (domainName.Domains.Length == 0 || string.IsNullOrWhiteSpace(domainName.Domains[0]))
                throw new ValidationException("域名不能为空");

            domainName.Domains = domainName.Domains.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            domainName.Domains = domainName.Domains.Distinct().ToArray();

            configService.AddDomainName(domainName);
        }).WithDescription("创建域名").WithDisplayName("创建域名").WithTags("域名");

        domain.MapGet("{serviceId}", (ConfigurationService configService, string serviceId) =>
            {
                var result = configService.GetDomainNamesByServerId(serviceId);

                return result;
            })
            .WithDescription("获取域名列表")
            .WithDisplayName("获取域名列表")
            .WithTags("域名");

        domain.MapDelete("{id}",
                (ConfigurationService configService, string id) => { configService.DeleteDomainName(id); })
            .WithDescription("删除域名").WithDisplayName("删除域名").WithTags("域名");

        domain.MapPut("{id}", (ConfigurationService configService, string id, DomainName domainName) =>
        {
            if (domainName.Domains.Length == 0 || string.IsNullOrWhiteSpace(domainName.Domains[0]))
                throw new ValidationException("域名不能为空");

            domainName.Domains = domainName.Domains.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            domainName.Domains = domainName.Domains.Distinct().ToArray();

            domainName.Id = id;

            configService.UpdateDomainName(domainName);
        }).WithDescription("更新域名").WithDisplayName("更新域名").WithTags("域名");

        // 检查目录或文件是否存在
        domain.MapPost("check", (CheckInput input) =>
        {
            if (string.IsNullOrWhiteSpace(input.Path)) return ResultDto.CreateSuccess(false);

            return File.Exists(input.Path) || Directory.Exists(input.Path)
                ? ResultDto.CreateSuccess(true)
                : ResultDto.CreateFailed(string.Empty);
        }).WithDescription("检查目录或文件是否存在").WithDisplayName("检查目录或文件是否存在").WithTags("域名");

        // 检查服务是否可用
        domain.MapPost("check/service", async (CheckInput input, IHttpClientFactory httpClientFactory) =>
        {
            if (string.IsNullOrWhiteSpace(input.Path)) return ResultDto.CreateSuccess();

            try
            {
                var httpClient = httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                var response = await httpClient.GetAsync(input.Path);

                return (int)response.StatusCode >= 500
                    ? ResultDto.CreateFailed(await response.Content.ReadAsStringAsync())
                    : ResultDto.CreateSuccess(true);
            }
            catch (Exception e)
            {
                return ResultDto.CreateFailed(e.Message);
            }
        }).WithDescription("检查服务是否可用").WithDisplayName("检查服务是否可用").WithTags("域名");

        domain.MapPut("{id}/enable", (ConfigurationService configService, string id) =>
        {
            var domainNames = configService.GetDomainNames();
            var domainName = domainNames.FirstOrDefault(x => x.Id == id);
            if (domainName != null)
            {
                domainName.Enable = !domainName.Enable;
                configService.UpdateDomainName(domainName);
            }
        }).WithDescription("启用/禁用域名").WithDisplayName("启用/禁用域名").WithTags("域名");


        return app;
    }
}