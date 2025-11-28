using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 表示单个摆轮的配置条目，用于LiteDB存储
/// </summary>
public class DiverterConfigurationEntry
{
    /// <summary>
    /// 摆轮标识（数字ID，与硬件设备对应）
    /// </summary>
    public required long DiverterId { get; set; }

    /// <summary>
    /// 摆轮名称（可选）- Diverter Name (Optional)
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "DIV-001"、"1号摆轮"
    /// </remarks>
    public string? DiverterName { get; set; }

    /// <summary>
    /// 目标摆轮转向方向
    /// </summary>
    /// <remarks>
    /// 可选值:
    /// - Straight (0): 直行通过
    /// - Left (1): 转向左侧格口
    /// - Right (2): 转向右侧格口
    /// </remarks>
    public required DiverterDirection TargetDirection { get; set; }

    /// <summary>
    /// 段的顺序号，从1开始
    /// </summary>
    public required int SequenceNumber { get; set; }

    /// <summary>
    /// 到达本摆轮的皮带段长度（毫米）- Segment Belt Length (mm)
    /// </summary>
    /// <remarks>
    /// 从上一个摆轮（或入口）到本摆轮的距离，用于计算包裹到达时间和段TTL
    /// </remarks>
    public double SegmentLengthMm { get; set; } = 5000.0;

    /// <summary>
    /// 本段皮带速度（毫米/秒）- Segment Belt Speed (mm/s)
    /// </summary>
    /// <remarks>
    /// 本段输送带的运行速度，用于计算包裹通过本段的时间
    /// </remarks>
    public double SegmentSpeedMmPerSecond { get; set; } = 1000.0;

    /// <summary>
    /// 本段容差时间（毫秒）- Segment Tolerance Time (ms)
    /// </summary>
    /// <remarks>
    /// <para>允许的时间误差范围，用于计算段TTL</para>
    /// <para>段TTL = (段长度 / 段速度) * 1000 + 容差时间</para>
    /// <para><strong>重要：</strong>容差时间应该合理设置，以避免相邻包裹的超时检测窗口重叠。</para>
    /// <para><strong>推荐配置：</strong>容差时间应小于包裹间隔时间的一半（容差 &lt; 包裹间隔/2）</para>
    /// <para><strong>示例：</strong></para>
    /// <list type="bullet">
    /// <item>包裹间隔1000ms → 容差应 &lt; 500ms</item>
    /// <item>包裹间隔2000ms → 容差应 &lt; 1000ms</item>
    /// <item>包裹间隔500ms → 容差应 &lt; 250ms</item>
    /// </list>
    /// <para>可使用 <see cref="DefaultSwitchingPathGenerator.ValidateToleranceTime"/> 方法验证配置是否合理</para>
    /// </remarks>
    public int SegmentToleranceTimeMs { get; set; } = 2000;
}
