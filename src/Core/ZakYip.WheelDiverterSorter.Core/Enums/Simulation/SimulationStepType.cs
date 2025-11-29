namespace ZakYip.WheelDiverterSorter.Core.Enums.Simulation;

/// <summary>
/// 模拟步骤类型
/// </summary>
/// <remarks>
/// 定义拓扑模拟测试中的步骤类型。
/// 此枚举属于 Core 层的 Simulation 枚举目录。
/// </remarks>
public enum SimulationStepType
{
    /// <summary>
    /// 包裹创建
    /// </summary>
    ParcelCreation,

    /// <summary>
    /// 路由请求
    /// </summary>
    RoutingRequest,

    /// <summary>
    /// 运输中
    /// </summary>
    Transit,

    /// <summary>
    /// 传感器检测
    /// </summary>
    SensorDetection,

    /// <summary>
    /// 摆轮动作
    /// </summary>
    DiverterAction,

    /// <summary>
    /// 到达格口
    /// </summary>
    ChuteArrival
}
