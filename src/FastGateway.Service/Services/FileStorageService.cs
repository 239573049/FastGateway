using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Runtime.InteropServices;
using FastGateway.Service.Dto;
using FastGateway.Service.Infrastructure;

namespace FastGateway.Service.Services;

public static class FileStorageService
{
    static string GetDriveLetter(string path)
    {
        // 获取完整路径
        string fullPath = Path.GetFullPath(path);

        // 获取盘符
        string driveLetter = Path.GetPathRoot(fullPath);

        return driveLetter;
    }

    // 递归添加目录到ZIP文件
    static void AddDirectoryToZip(ZipArchive zip, string directoryPath, string entryName)
    {
        var files = Directory.GetFiles(directoryPath);
        var directories = Directory.GetDirectories(directoryPath);

        // 添加文件
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var entryPath = Path.Combine(entryName, fileName).Replace('\\', '/');
            zip.CreateEntryFromFile(file, entryPath);
        }

        // 递归添加子目录
        foreach (var directory in directories)
        {
            var dirName = Path.GetFileName(directory);
            var subEntryName = Path.Combine(entryName, dirName).Replace('\\', '/');
            AddDirectoryToZip(zip, directory, subEntryName);
        }

        // 如果目录为空，创建一个空的目录条目
        if (files.Length == 0 && directories.Length == 0)
        {
            var emptyDirEntry = entryName.Replace('\\', '/') + "/";
            zip.CreateEntry(emptyDirEntry);
        }
    }

    // 递归复制目录
    static void CopyDirectory(string sourceDir, string targetDir)
    {
        var source = new DirectoryInfo(sourceDir);
        var target = new DirectoryInfo(targetDir);

        // 创建目标目录
        if (!target.Exists)
        {
            target.Create();
        }

        // 复制所有文件
        foreach (var file in source.GetFiles())
        {
            var targetFile = Path.Combine(target.FullName, file.Name);
            file.CopyTo(targetFile, true);
        }

        // 递归复制子目录
        foreach (var subDir in source.GetDirectories())
        {
            var targetSubDir = Path.Combine(target.FullName, subDir.Name);
            CopyDirectory(subDir.FullName, targetSubDir);
        }
    }

    public static IEndpointRouteBuilder MapFileStorage(this IEndpointRouteBuilder app)
    {
        var fileStorage = app.MapGroup("/api/v1/filestorage")
            .WithTags("文件存储")
            .WithDescription("文件存储管理")
            .RequireAuthorization()
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("文件存储");

        // 获取系统盘符
        fileStorage.MapGet("drives", () =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return DriveInfo.GetDrives().Select(x => new DriveInfoDto()
                    {
                        Name = x.Name,
                        DriveType = x.DriveType.ToString(),
                        AvailableFreeSpace = x.AvailableFreeSpace,
                        TotalSize = x.TotalSize,
                        VolumeLabel = x.VolumeLabel,
                        DriveFormat = x.DriveFormat,
                        IsReady = x.IsReady
                    });
                }

                return DriveInfo.GetDrives().Where(x => x.Name == "/").Select(x => new DriveInfoDto()
                {
                    Name = x.Name,
                    DriveType = x.DriveType.ToString(),
                    AvailableFreeSpace = x.AvailableFreeSpace,
                    TotalSize = x.TotalSize,
                    VolumeLabel = x.VolumeLabel,
                    DriveFormat = x.DriveFormat,
                    IsReady = x.IsReady
                });
            })
            .WithDescription("获取系统盘符")
            .WithDisplayName("获取系统盘符").WithTags("文件存储");

        // 获取文件夹
        fileStorage.MapGet("directory", (string path, string drives) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ValidationException("路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            path = Path.Combine(drives, path.TrimStart('/'));

            var directory = new DirectoryInfo(path);
            var directories = directory.GetDirectories();
            var files = directory.GetFiles();
            return new
            {
                directories = directories.Select(x => new DirectoryInfoDto()
                {
                    Name = x.Name,
                    FullName = x.FullName,
                    Extension = x.Extension,
                    CreationTime = x.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    LastAccessTime = x.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    LastWriteTime = x.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Length = 0,
                    IsHidden = x.Attributes.HasFlag(FileAttributes.Hidden),
                    IsSystem = x.Attributes.HasFlag(FileAttributes.System),
                    IsDirectory = x.Attributes.HasFlag(FileAttributes.Directory),
                    IsFile = x.Attributes.HasFlag(FileAttributes.Archive),
                    Drive = GetDriveLetter(x.FullName)
                }).ToArray(),
                files = files.Select(x => new FileInfoDto()
                {
                    Name = x.Name,
                    FullName = x.FullName,
                    Extension = x.Extension,
                    CreationTime = x.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    LastAccessTime = x.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    LastWriteTime = x.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Length = x.Length,
                    IsReadOnly = x.IsReadOnly,
                    IsHidden = x.Attributes.HasFlag(FileAttributes.Hidden),
                    IsSystem = x.Attributes.HasFlag(FileAttributes.System),
                    IsDirectory = x.Attributes.HasFlag(FileAttributes.Directory),
                    IsFile = x.Attributes.HasFlag(FileAttributes.Archive),
                    Drive = GetDriveLetter(x.FullName)
                }).ToArray()
            };
        }).WithDescription("获取文件夹").WithDisplayName("获取文件夹").WithTags("文件存储");

        // 上传文件
        fileStorage.MapPost("upload", async (string path, string drives, HttpContext context) =>
        {
            var file = context.Request.Form.Files.FirstOrDefault();

            if (file == null)
            {
                throw new ValidationException("文件不能为空");
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ValidationException("路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            path = Path.Combine(drives, path.TrimStart('/'));

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var filePath = Path.Combine(path, file.FileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
        }).WithDescription("上传文件").WithDisplayName("上传文件").WithTags("文件存储");

        // 下载文件
        fileStorage.MapGet("download", async (string path, string drives, HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ValidationException("路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            path = Path.Combine(drives, path.TrimStart('/'));

            if (!File.Exists(path))
            {
                throw new ValidationException("文件不存在");
            }

            await context.Response.SendFileAsync(path);
        }).WithDescription("下载文件").WithDisplayName("下载文件").WithTags("文件存储");

        // 上传文件/使用俩个接口实现切片上传
        fileStorage.MapPost("upload/chunk", async (IFormFile file, string path, string drives, int index, int total) =>
        {
            if (file == null)
            {
                throw new ValidationException("文件不能为空");
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ValidationException("路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            path = Path.Combine(drives, path.TrimStart('/'));

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var filePath = Path.Combine(path, file.FileName);
            await using var stream = new FileStream(filePath, index == 0 ? FileMode.Create : FileMode.Append);
            await file.CopyToAsync(stream);
        }).WithDescription("上传文件").WithDisplayName("上传文件").WithTags("文件存储");

        fileStorage.MapPost("upload/merge", async (string path, string drives, string fileName) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ValidationException("路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            path = Path.Combine(drives, path.TrimStart('/'));

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var filePath = Path.Combine(path, fileName);
            var files = Directory.GetFiles(path, $"{fileName}.*");
            if (files.Length == 0)
            {
                throw new ValidationException("文件不存在");
            }

            await using var stream = new FileStream(filePath, FileMode.Create);
            foreach (var file in files.OrderBy(x => x))
            {
                await using var fs = new FileStream(file, FileMode.Open);
                await fs.CopyToAsync(stream);
            }

            foreach (var file in files)
            {
                File.Delete(file);
            }
        }).WithDescription("合并文件").WithDisplayName("合并文件").WithTags("文件存储");

        // 解压指定的zip文件
        fileStorage.MapPost("unzip", (UnzipRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Path))
            {
                throw new ValidationException("ZIP文件路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.Drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            var zipPath = Path.Combine(request.Drives, request.Path.TrimStart('/'));

            if (!File.Exists(zipPath))
            {
                throw new ValidationException("ZIP文件不存在");
            }

            // 检查文件是否为zip文件
            if (!zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("文件不是ZIP格式");
            }

            // 解压到同目录下，创建以文件名命名的文件夹
            var directory = Path.GetDirectoryName(zipPath);
            var extractDirectory = Path.Combine(directory, Path.GetFileNameWithoutExtension(zipPath));
            
            // 如果解压目录已存在，先删除
            if (Directory.Exists(extractDirectory))
            {
                Directory.Delete(extractDirectory, true);
            }

            Directory.CreateDirectory(extractDirectory);
            
            try
            {
                ZipFile.ExtractToDirectory(zipPath, extractDirectory);
            }
            catch (Exception ex)
            {
                // 如果解压失败，清理创建的目录
                if (Directory.Exists(extractDirectory))
                {
                    Directory.Delete(extractDirectory, true);
                }
                throw new ValidationException($"解压失败: {ex.Message}");
            }
        }).WithDescription("解压ZIP文件").WithDisplayName("解压ZIP文件").WithTags("文件存储");

        // 批量打包文件为ZIP
        fileStorage.MapPost("create-zip", (CreateZipRequest request) =>
        {
            if (request.SourcePaths == null || request.SourcePaths.Length == 0)
            {
                throw new ValidationException("源文件路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.Drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.ZipName))
            {
                throw new ValidationException("ZIP文件名不能为空");
            }

            // 确保ZIP文件名以.zip结尾
            if (!request.ZipName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                request.ZipName += ".zip";
            }

            // 确定ZIP文件的输出路径
            var firstSourcePath = Path.Combine(request.Drives, request.SourcePaths[0].TrimStart('/'));
            var outputDirectory = Path.GetDirectoryName(firstSourcePath);
            var zipPath = Path.Combine(outputDirectory, request.ZipName);

            // 如果ZIP文件已存在，先删除
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            // 创建ZIP文件
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            
            foreach (var sourcePath in request.SourcePaths)
            {
                var fullPath = Path.Combine(request.Drives, sourcePath.TrimStart('/'));
                
                if (File.Exists(fullPath))
                {
                    // 添加文件到ZIP
                    var fileName = Path.GetFileName(fullPath);
                    zip.CreateEntryFromFile(fullPath, fileName);
                }
                else if (Directory.Exists(fullPath))
                {
                    // 添加目录到ZIP
                    AddDirectoryToZip(zip, fullPath, Path.GetFileName(fullPath));
                }
            }
        }).WithDescription("批量打包文件为ZIP").WithDisplayName("批量打包文件为ZIP").WithTags("文件存储");

        // 打包单个文件或文件夹为ZIP
        fileStorage.MapPost("create-zip-from-path", (CreateZipFromPathRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.SourcePath))
            {
                throw new ValidationException("源路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.Drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.ZipName))
            {
                throw new ValidationException("ZIP文件名不能为空");
            }

            // 确保ZIP文件名以.zip结尾
            if (!request.ZipName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                request.ZipName += ".zip";
            }

            var sourcePath = Path.Combine(request.Drives, request.SourcePath.TrimStart('/'));
            var outputDirectory = Path.GetDirectoryName(sourcePath);
            var zipPath = Path.Combine(outputDirectory, request.ZipName);

            // 如果ZIP文件已存在，先删除
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            // 创建ZIP文件
            if (File.Exists(sourcePath))
            {
                // 打包单个文件
                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                var fileName = Path.GetFileName(sourcePath);
                zip.CreateEntryFromFile(sourcePath, fileName);
            }
            else if (Directory.Exists(sourcePath))
            {
                // 打包整个目录
                ZipFile.CreateFromDirectory(sourcePath, zipPath);
            }
            else
            {
                throw new ValidationException("源路径不存在");
            }
        }).WithDescription("打包单个文件或文件夹为ZIP").WithDisplayName("打包单个文件或文件夹为ZIP").WithTags("文件存储");

        // 删除文件/文件夹

        fileStorage.MapDelete("delete", (string path, string drives) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ValidationException("路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            path = Path.Combine(drives, path.TrimStart('/'));

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }).WithDescription("删除文件/文件夹").WithDisplayName("删除文件/文件夹").WithTags("文件存储");


        // 重命名
        fileStorage.MapPut("rename", (string path, string drives, string name) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ValidationException("路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ValidationException("名称不能为空");
            }

            path = Path.Combine(drives, path.TrimStart('/'));

            if (File.Exists(path))
            {
                var directory = Path.GetDirectoryName(path);
                var newPath = Path.Combine(directory, name);
                File.Move(path, newPath);
            }
            else if (Directory.Exists(path))
            {
                var directory = Path.GetDirectoryName(path);
                var newPath = Path.Combine(directory, name);
                Directory.Move(path, newPath);
            }
        }).WithDescription("重命名").WithDisplayName("重命名").WithTags("文件存储");

        fileStorage.MapPut("create-directory", (string path, string drives, string name) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ValidationException("路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            path = Path.Combine(drives, path.TrimStart('/'), name);

            if (File.Exists(path))
            {
                throw new ValidationException("文件已存在");
            }

            if (Directory.Exists(path))
            {
                throw new ValidationException("文件夹已存在");
            }

            Directory.CreateDirectory(path);
        }).WithDescription("创建文件/文件夹").WithDisplayName("创建文件/文件夹").WithTags("文件存储");

        fileStorage.MapGet("property", (string path) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return ResultDto.CreateFailed("路径不能为空");
            }

            if (File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                var fileProperties = new
                {
                    Type = "File",
                    fileInfo.Name,
                    fileInfo.FullName,
                    fileInfo.Extension,
                    fileInfo.Length,
                    fileInfo.CreationTime,
                    fileInfo.LastAccessTime,
                    fileInfo.LastWriteTime
                };
                return ResultDto.CreateSuccess(fileProperties);
            }
            else if (Directory.Exists(path))
            {
                var directoryInfo = new DirectoryInfo(path);
                var directoryProperties = new
                {
                    Type = "Directory",
                    directoryInfo.Name,
                    directoryInfo.FullName,
                    directoryInfo.CreationTime,
                    directoryInfo.LastAccessTime,
                    directoryInfo.LastWriteTime
                };
                return ResultDto.CreateSuccess(directoryProperties);
            }
            else
            {
                return ResultDto.CreateFailed("路径不存在");
            }
        }).WithDescription("获取文件或目录属性").WithDisplayName("获取文件或目录属性").WithTags("文件存储");

        // 批量删除文件
        fileStorage.MapPost("delete-multiple", (DeleteMultipleRequest request) =>
        {
            if (request.Items == null || request.Items.Length == 0)
            {
                throw new ValidationException("删除项不能为空");
            }

            var errors = new List<string>();
            var successCount = 0;

            foreach (var item in request.Items)
            {
                try
                {
                    var path = Path.Combine(item.Drives, item.Path.TrimStart('/'));
                    
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        successCount++;
                    }
                    else if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                        successCount++;
                    }
                    else
                    {
                        errors.Add($"路径不存在: {item.Path}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"删除失败 {item.Path}: {ex.Message}");
                }
            }

            if (errors.Any())
            {
                throw new ValidationException($"批量删除完成，成功: {successCount}，失败: {errors.Count}。错误: {string.Join("; ", errors)}");
            }
        }).WithDescription("批量删除文件").WithDisplayName("批量删除文件").WithTags("文件存储");

        // 移动文件或文件夹
        fileStorage.MapPost("move", (MoveFileRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.SourcePath))
            {
                throw new ValidationException("源路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.TargetPath))
            {
                throw new ValidationException("目标路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.Drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            var sourcePath = Path.Combine(request.Drives, request.SourcePath.TrimStart('/'));
            var targetPath = Path.Combine(request.Drives, request.TargetPath.TrimStart('/'));

            // 确保目标目录存在
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, targetPath);
            }
            else if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, targetPath);
            }
            else
            {
                throw new ValidationException("源路径不存在");
            }
        }).WithDescription("移动文件或文件夹").WithDisplayName("移动文件或文件夹").WithTags("文件存储");

        // 复制文件或文件夹
        fileStorage.MapPost("copy", (CopyFileRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.SourcePath))
            {
                throw new ValidationException("源路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.TargetPath))
            {
                throw new ValidationException("目标路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.Drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            var sourcePath = Path.Combine(request.Drives, request.SourcePath.TrimStart('/'));
            var targetPath = Path.Combine(request.Drives, request.TargetPath.TrimStart('/'));

            // 确保目标目录存在
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetPath, true);
            }
            else if (Directory.Exists(sourcePath))
            {
                CopyDirectory(sourcePath, targetPath);
            }
            else
            {
                throw new ValidationException("源路径不存在");
            }
        }).WithDescription("复制文件或文件夹").WithDisplayName("复制文件或文件夹").WithTags("文件存储");

        // 创建文件
        fileStorage.MapPost("create-file", (CreateFileRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Path))
            {
                throw new ValidationException("路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.Drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                throw new ValidationException("文件名不能为空");
            }

            var directoryPath = Path.Combine(request.Drives, request.Path.TrimStart('/'));
            var filePath = Path.Combine(directoryPath, request.FileName);

            // 确保目录存在
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // 创建文件
            File.WriteAllText(filePath, request.Content ?? string.Empty);
        }).WithDescription("创建文件").WithDisplayName("创建文件").WithTags("文件存储");

        // 获取文件内容
        fileStorage.MapGet("content", (string path, string drives) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ValidationException("路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            var filePath = Path.Combine(drives, path.TrimStart('/'));

            if (!File.Exists(filePath))
            {
                throw new ValidationException("文件不存在");
            }

            var content = File.ReadAllText(filePath);
            return new { content };
        }).WithDescription("获取文件内容").WithDisplayName("获取文件内容").WithTags("文件存储");

        // 保存文件内容
        fileStorage.MapPost("save-content", (SaveContentRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Path))
            {
                throw new ValidationException("路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(request.Drives))
            {
                throw new ValidationException("盘符不能为空");
            }

            var filePath = Path.Combine(request.Drives, request.Path.TrimStart('/'));

            if (!File.Exists(filePath))
            {
                throw new ValidationException("文件不存在");
            }

            File.WriteAllText(filePath, request.Content ?? string.Empty);
        }).WithDescription("保存文件内容").WithDisplayName("保存文件内容").WithTags("文件存储");

        return app;
    }
}