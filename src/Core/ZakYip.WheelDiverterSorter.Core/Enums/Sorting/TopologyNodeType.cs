namespace ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

/// <summary>
/// 拓扑节点类型
/// </summary>
/// <remarks>
/// 定义线体拓扑中各种节点的类型，用于区分不同的物理设备或逻辑位置。
/// </remarks>
public enum TopologyNodeType
{
    /// <summary>
    /// 进料点（入口传感器位置）
    /// </summary>
    Induction = 0,

    /// <summary>
    /// 摆轮分拣器
    /// </summary>
    WheelDiverter = 1,

    /// <summary>
    /// 输送线段
    /// </summary>
    ConveyorSegment = 2,

    /// <summary>
    /// 格口
    /// </summary>
    Chute = 3,

    /// <summary>
    /// 传感器
    /// </summary>
    Sensor = 4
}
