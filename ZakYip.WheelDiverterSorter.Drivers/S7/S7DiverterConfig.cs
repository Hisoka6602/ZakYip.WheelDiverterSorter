namespace ZakYip.WheelDiverterSorter.Drivers.S7;

/// <summary>
/// S7 摆轮配置
/// </summary>
public class S7DiverterConfig
{
    /// <summary>
    /// 摆轮ID
    /// </summary>
    public string DiverterId { get; set; } = string.Empty;

    /// <summary>
    /// 输出数据块编号
    /// </summary>
    public int OutputDbNumber { get; set; }

    /// <summary>
    /// 输出起始字节地址
    /// </summary>
    public int OutputStartByte { get; set; }

    /// <summary>
    /// 输出起始位地址（0-7）
    /// </summary>
    public int OutputStartBit { get; set; }

    /// <summary>
    /// 反馈输入数据块编号（可选）
    /// </summary>
    public int? FeedbackInputDbNumber { get; set; }

    /// <summary>
    /// 反馈输入字节地址（可选）
    /// </summary>
    public int? FeedbackInputByte { get; set; }

    /// <summary>
    /// 反馈输入位地址（可选，0-7）
    /// </summary>
    public int? FeedbackInputBit { get; set; }
}
