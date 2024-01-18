namespace Gateway.Options;

public class RequestOptions
{
    /// <summary>
    /// 过滤后缀
    /// </summary>
    public static string[] FilterSuffixes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 需要过滤的ContentType 
    /// </summary>
    public static string[] FilterContentTypes { get; set; } = Array.Empty<string>();
}