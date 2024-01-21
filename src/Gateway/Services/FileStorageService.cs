namespace Gateway.Services;

public class FileStorageService
{
    public async Task<ResultDto<string>> UploadAsync(HttpContext context)
    {
        var file = context.Request.Form.Files.FirstOrDefault();
        var filePath = $"{DateTime.Now:HHmmss}" + file!.FileName;
        var path = "/data/";
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