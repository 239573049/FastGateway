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

        // 解压zip后缀名的文件
        fileStorage.MapPost("unzip", (string path, string drives) =>
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
                throw new ValidationException("文件夹不存在");
            }

            var files = Directory.GetFiles(path, "*.zip");
            if (files.Length == 0)
            {
                throw new ValidationException("文件不存在");
            }

            foreach (var file in files)
            {
                var directory = Path.Combine(path, Path.GetFileNameWithoutExtension(file));
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                ZipFile.ExtractToDirectory(file, directory);
                File.Delete(file);
            }
        }).WithDescription("解压文件").WithDisplayName("解压文件").WithTags("文件存储");

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

        return app;
    }
}