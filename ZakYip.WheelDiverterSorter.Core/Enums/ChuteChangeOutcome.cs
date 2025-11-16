namespace ZakYip.WheelDiverterSorter.Core.Enums;

/// <summary>
/// 改口请求的决策结果。
/// </summary>
public enum ChuteChangeOutcome
{
    /// <summary>改口已接受并生效。</summary>
    Accepted,

    /// <summary>改口被忽略，因为路由已完成。</summary>
    IgnoredAlreadyCompleted,

    /// <summary>改口被忽略，因为已进入异常路径。</summary>
    IgnoredExceptionRouted,

    /// <summary>改口被拒绝，因为当前状态不允许（例如已失败或已废弃）。</summary>
    RejectedInvalidState,

    /// <summary>改口被拒绝，因为当前时刻已过可重规划时间点。</summary>
    RejectedTooLate
}
