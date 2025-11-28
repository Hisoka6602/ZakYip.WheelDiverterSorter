// This file is maintained for backward compatibility.
// The enum has been moved to ZakYip.WheelDiverterSorter.Core.Enums.System namespace.

// For backward compatibility, use the type from Core.Enums.System
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Host.StateMachine;

/// <summary>
/// 启动阶段信息
/// Bootstrap Stage Information
/// </summary>
public record class BootstrapStageInfo
{
    /// <summary>当前阶段</summary>
    public required BootstrapStage Stage { get; init; }

    /// <summary>进入当前阶段的时间</summary>
    public required DateTimeOffset EnteredAt { get; init; }

    /// <summary>阶段描述或额外信息</summary>
    public string? Message { get; init; }

    /// <summary>是否成功</summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>失败原因（如果失败）</summary>
    public string? FailureReason { get; init; }
}
