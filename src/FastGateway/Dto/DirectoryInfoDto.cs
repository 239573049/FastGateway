namespace FastGateway.Dto;

public class DirectoryInfoDto
{
    public string Name { get; set; }

    public string FullName { get; set; }

    public string Extension { get; set; }

    public string CreationTime { get; set; }

    public string LastAccessTime { get; set; }

    public string LastWriteTime { get; set; }

    public long Length { get; set; }

    public bool IsReadOnly { get; set; }

    public bool IsHidden { get; set; }

    public bool IsSystem { get; set; }

    public bool IsDirectory { get; set; }

    public bool IsFile { get; set; }

    /// <summary>
    ///     盘符
    /// </summary>
    public string Drive { get; set; }
}