using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Monitoring;

/// <summary>
/// 诊断级别 / Diagnostics Level
/// </summary>
/// <remarks>
/// 控制系统的诊断详细程度，影响日志量和追踪记录的详细程度。
/// Controls the verbosity of system diagnostics, affecting log volume and trace detail.
/// </remarks>
public enum DiagnosticsLevel
{
    /// <summary>
    /// 关闭诊断，仅保留必要错误日志。
    /// Diagnostics disabled, only essential error logs are kept.
    /// </summary>
    [Description("关闭诊断，仅记录错误日志")]
    None = 0,

    /// <summary>
    /// 基本诊断，记录关键状态与异常信息。
    /// Basic diagnostics, logs key states and exception info.
    /// </summary>
    [Description("基本诊断，记录关键状态与异常信息")]
    Basic = 1,

    /// <summary>
    /// 详细诊断，记录完整流水线关键步骤。
    /// Verbose diagnostics, logs complete pipeline key steps.
    /// </summary>
    [Description("详细诊断，记录完整流水线关键步骤")]
    Verbose = 2
}
