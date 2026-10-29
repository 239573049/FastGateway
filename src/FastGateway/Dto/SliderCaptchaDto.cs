namespace FastGateway.Dto;

/// <summary>滑块挑战：图片为 data URL，缺口坐标不下发</summary>
public sealed class SliderChallengeDto
{
    public string Id { get; set; } = string.Empty;

    public string Background { get; set; } = string.Empty;

    /// <summary>拼图块图片，宽为 <see cref="PieceSize" />、高与背景一致，纵向位置已画在图内</summary>
    public string Piece { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public int PieceSize { get; set; }
}

public sealed class SliderVerifyRequest
{
    public string Id { get; set; } = string.Empty;

    /// <summary>拼图块最终左边距，单位为背景图像素</summary>
    public int X { get; set; }

    public List<SliderTrackPoint>? Track { get; set; }
}

/// <summary>拖动轨迹采样点：X/Y 为相对起点的位移（背景图像素），T 为相对按下时刻的毫秒数</summary>
public sealed class SliderTrackPoint
{
    public int X { get; set; }

    public int Y { get; set; }

    public int T { get; set; }
}
