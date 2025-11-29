namespace ZakYip.WheelDiverterSorter.Core.Enums.Simulation;

/// <summary>
/// 步骤状态
/// </summary>
/// <remarks>
/// 定义模拟测试中各步骤的执行状态。
/// 此枚举属于 Core 层的 Simulation 枚举目录。
/// </remarks>
public enum StepStatus
{
    /// <summary>
    /// 成功
    /// </summary>
    Success,

    /// <summary>
    /// 失败
    /// </summary>
    Failed,

    /// <summary>
    /// 超时
    /// </summary>
    Timeout,

    /// <summary>
    /// 路由到异常格口
    /// </summary>
    RoutedToException
}
