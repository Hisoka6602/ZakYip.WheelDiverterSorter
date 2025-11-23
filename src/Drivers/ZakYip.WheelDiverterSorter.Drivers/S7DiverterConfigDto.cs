namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// S7 摆轮配置 DTO
/// </summary>
public record S7DiverterConfigDto
{
    /// <summary>
    /// 摆轮ID
    /// </summary>
    public required string DiverterId { get; init; }

    /// <summary>
    /// 输出数据块编号（DB编号）
    /// </summary>
    public required int OutputDbNumber { get; init; }

    /// <summary>
    /// 输出起始字节地址
    /// </summary>
    public required int OutputStartByte { get; init; }

    /// <summary>
    /// 输出起始位地址（0-7）
    /// </summary>
    public required int OutputStartBit { get; init; }

    /// <summary>
    /// 反馈输入数据块编号（可选）
    /// </summary>
    public int? FeedbackInputDbNumber { get; init; }

    /// <summary>
    /// 反馈输入字节地址（可选）
    /// </summary>
    public int? FeedbackInputByte { get; init; }

    /// <summary>
    /// 反馈输入位地址（可选，0-7）
    /// </summary>
    public int? FeedbackInputBit { get; init; }
}
