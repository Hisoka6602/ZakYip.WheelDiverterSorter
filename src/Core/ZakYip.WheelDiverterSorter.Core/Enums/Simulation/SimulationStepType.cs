using System.ComponentModel;

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
    [Description("包裹创建")]
    ParcelCreation,

    /// <summary>
    /// 路由请求
    /// </summary>
    [Description("路由请求")]
    RoutingRequest,

    /// <summary>
    /// 运输中
    /// </summary>
    [Description("运输中")]
    Transit,

    /// <summary>
    /// 传感器检测
    /// </summary>
    [Description("传感器检测")]
    SensorDetection,

    /// <summary>
    /// 摆轮动作
    /// </summary>
    [Description("摆轮动作")]
    DiverterAction,

    /// <summary>
    /// 到达格口
    /// </summary>
    [Description("到达格口")]
    ChuteArrival
}
