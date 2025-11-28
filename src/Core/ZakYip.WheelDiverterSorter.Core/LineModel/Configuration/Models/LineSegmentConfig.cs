namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 线体段配置
/// </summary>
/// <remarks>
/// 描述线体上两个感应IO之间的物理段，包括长度和速度信息。
/// 
/// **拓扑规则**：
/// - 线体段由起点IO和终点IO定义，通过感应IO的Id引用
/// - 第一段线体的起点IO必须是创建包裹感应IO（ParcelCreation类型）
/// - 最后一段线体的终点IO Id应该设为0，表示已到达末端
/// - 中间线体段的终点IO通常是摆轮前感应IO（WheelFront类型）
/// 
/// **计算用途**：
/// - 根据线体长度和速度计算包裹从上一个IO到下一个IO的理论时间
/// - 加上容差时间（考虑包裹摩擦力等因素）
/// - 用于超时检测和丢包判断逻辑
/// 
/// **通过时间计算公式**：
/// ```
/// 理论通过时间(ms) = (长度mm / 速度mm/s) * 1000
/// 实际通过时间(ms) = 理论通过时间 + 容差时间
/// ```
/// </remarks>
public record class LineSegmentConfig
{
    /// <summary>
    /// 线体段唯一标识符
    /// </summary>
    public required long SegmentId { get; init; }

    /// <summary>
    /// 线体段显示名称
    /// </summary>
    /// <example>入口到第一摆轮段</example>
    public string? SegmentName { get; init; }

    /// <summary>
    /// 起点感应IO的Id（引用感应IO配置中的SensorId）
    /// </summary>
    /// <remarks>
    /// 第一段线体的起点IO必须是创建包裹感应IO（ParcelCreation类型）。
    /// </remarks>
    public required long StartIoId { get; init; }

    /// <summary>
    /// 终点感应IO的Id（引用感应IO配置中的SensorId）
    /// </summary>
    /// <remarks>
    /// - 中间线体段：通常是摆轮前感应IO（WheelFront类型）
    /// - 最后一段线体：设为0，表示已到达末端
    /// </remarks>
    public required long EndIoId { get; init; }

    /// <summary>
    /// 线体段物理长度（单位：毫米）
    /// </summary>
    /// <remarks>
    /// 从起点IO到终点IO的实际距离
    /// </remarks>
    public required double LengthMm { get; init; }

    /// <summary>
    /// 线体运行速度（单位：毫米/秒）
    /// </summary>
    /// <remarks>
    /// 该线体段的标准运行速度，用于计算理论到达时间。
    /// </remarks>
    public required double SpeedMmPerSec { get; init; }

    /// <summary>
    /// 容差时间（单位：毫秒）
    /// </summary>
    /// <remarks>
    /// 考虑包裹摩擦力等因素的额外时间容差。
    /// 此值会加到理论通过时间上，得到实际预期通过时间。
    /// 默认值为200毫秒。
    /// 
    /// 注意：设置此值时需要验证不与下一个包裹产生重叠，
    /// 即：(理论通过时间 + 容差时间) 不应超过正常放包间隔(normalReleaseIntervalMs)
    /// </remarks>
    public double ToleranceTimeMs { get; init; } = 200.0;

    /// <summary>
    /// 线体段描述（可选）
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 计算包裹通过该段的理论时间（毫秒，不含容差）
    /// </summary>
    /// <param name="speedMmPerSec">实际速度，如果为null则使用配置的速度</param>
    /// <returns>通过该段所需的理论时间（毫秒）</returns>
    public double CalculateTransitTimeMs(double? speedMmPerSec = null)
    {
        var speed = speedMmPerSec ?? SpeedMmPerSec;
        if (speed <= 0)
        {
            throw new ArgumentException("速度必须大于0", nameof(speedMmPerSec));
        }
        return (LengthMm / speed) * 1000.0;
    }

    /// <summary>
    /// 计算包裹通过该段的实际预期时间（毫秒，含容差）
    /// </summary>
    /// <param name="speedMmPerSec">实际速度，如果为null则使用配置的速度</param>
    /// <returns>通过该段所需的实际预期时间（毫秒）= 理论时间 + 容差时间</returns>
    public double CalculateActualTransitTimeMs(double? speedMmPerSec = null)
    {
        return CalculateTransitTimeMs(speedMmPerSec) + ToleranceTimeMs;
    }

    /// <summary>
    /// 检查是否为末端线体段（终点IO Id为0）
    /// </summary>
    public bool IsEndSegment => EndIoId == 0;

    /// <summary>
    /// 验证容差时间是否与放包间隔冲突
    /// </summary>
    /// <param name="normalReleaseIntervalMs">正常放包间隔（毫秒）</param>
    /// <returns>验证结果和错误信息</returns>
    /// <remarks>
    /// 验证规则：(理论通过时间 + 容差时间) 不应超过正常放包间隔，
    /// 否则可能导致第二个包裹在第一个包裹到达前就被释放，产生重叠。
    /// </remarks>
    public (bool IsValid, string? ErrorMessage) ValidateToleranceAgainstReleaseInterval(double normalReleaseIntervalMs)
    {
        var actualTransitTime = CalculateActualTransitTimeMs();
        
        if (actualTransitTime > normalReleaseIntervalMs)
        {
            return (false, 
                $"线体段 {SegmentId} 的实际通过时间({actualTransitTime:F0}ms = 理论{CalculateTransitTimeMs():F0}ms + 容差{ToleranceTimeMs:F0}ms)" +
                $"超过了正常放包间隔({normalReleaseIntervalMs:F0}ms)，可能导致包裹重叠");
        }

        return (true, null);
    }
}
