namespace Gateway.Services;

public class FileStorageService
{
    public static async Task<ResultDto<string>> UploadAsync(HttpContext context)
    {
        var file = context.Request.Form.Files.FirstOrDefault();
        var filePath = $"{DateTime.Now:HHmmss}" + file!.FileName;
        const string path = "/data/";
        if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }

        // 使用管理员权限创建文件
        await using var stream = new FileStream(path + filePath, FileMode.Create, FileAccess.Write, FileShare.Write,
            4096, true);
        await file.CopyToAsync(stream);

        return ResultDto<string>.Success(filePath);
    }
}

public static class FileStorageExtension
{
    public static void MapFileStorage(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/gateway/file-storage", async (HttpContext context) =>
                await FileStorageService.UploadAsync(context))
            .RequireAuthorization();
    }
}