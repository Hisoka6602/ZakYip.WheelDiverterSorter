using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 中段皮带 IO 联动配置选项。
/// 定义多个中段皮带段的 IO 映射及联动策略。
/// </summary>
public sealed record class MiddleConveyorIoOptions
{
    /// <summary>
    /// 是否启用中段皮带 IO 联动功能
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 是否处于仿真模式（为仿真/生产环境切换预留）
    /// </summary>
    public bool IsSimulationMode { get; init; } = false;

    /// <summary>
    /// 所有中段皮带段的 IO 映射配置列表
    /// </summary>
    [Required(ErrorMessage = "皮带段配置列表不能为空")]
    public required IReadOnlyList<ConveyorIoMapping> Segments { get; init; }

    /// <summary>
    /// 停机顺序策略（"DownstreamFirst" = 先停下游，"UpstreamFirst" = 先停上游，"Simultaneous" = 同时停止）
    /// </summary>
    public string StopOrderStrategy { get; init; } = "DownstreamFirst";

    /// <summary>
    /// 启动顺序策略（"DownstreamFirst" = 先启下游，"UpstreamFirst" = 先启上游，"Simultaneous" = 同时启动）
    /// </summary>
    public string StartOrderStrategy { get; init; } = "UpstreamFirst";

    /// <summary>
    /// 联动操作之间的延迟时间（毫秒），用于顺序启停时的间隔
    /// </summary>
    [Range(0, 10000, ErrorMessage = "联动延迟时间必须在 0-10000 毫秒之间")]
    public int LinkageDelayMs { get; init; } = 500;
}
