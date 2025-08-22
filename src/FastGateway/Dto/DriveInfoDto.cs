namespace FastGateway.Dto;

public sealed class DriveInfoDto
{
    public string Name { get; set; }

    public string DriveType { get; set; }

    public long AvailableFreeSpace { get; set; }

    public long TotalSize { get; set; }

    public string VolumeLabel { get; set; }

    public string DriveFormat { get; set; }

    public bool IsReady { get; set; }
}