namespace Gateway.Services;

public class FileStorageService
{
    public async Task<ResultDto<string>> UploadAsync(HttpContext context)
    {
        var file = context.Request.Form.Files.FirstOrDefault();
        var filePath = $"certificates/{DateTime.Now:HHmmss}" + file.FileName;
        var path = Path.Combine(Directory.GetCurrentDirectory(), filePath);
        if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }

        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);

        return ResultDto<string>.Success(filePath);
    }
    
}