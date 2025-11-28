using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Execution.Diagnostics;

/// <summary>
/// 诊断配置选项
/// </summary>
public class DiagnosticsOptions
{
    /// <summary>
    /// 配置节点名称
    /// </summary>
    public const string SectionName = "Diagnostics";

    /// <summary>
    /// 诊断级别
    /// </summary>
    /// <remarks>
    /// 默认为 Basic
    /// </remarks>
    public DiagnosticsLevel Level { get; set; } = DiagnosticsLevel.Basic;

    /// <summary>
    /// 正常件抽样比例（当Level为Basic时生效）
    /// </summary>
    /// <remarks>
    /// 取值范围 0.0 ~ 1.0，默认 0.1（10%）
    /// </remarks>
    public double NormalParcelSamplingRate { get; set; } = 0.1;
}
