namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 摆轮设备状态
/// </summary>
/// <remarks>
/// 描述摆轮设备当前的运行状态。
/// 此枚举属于 Core 层的 Hardware 枚举目录。
/// </remarks>
public enum WheelDiverterState
{
    /// <summary>未知状态</summary>
    Unknown = 0,
    
    /// <summary>空闲/就绪</summary>
    Idle = 1,
    
    /// <summary>正在执行动作</summary>
    Executing = 2,
    
    /// <summary>处于左转位置</summary>
    AtLeft = 3,
    
    /// <summary>处于右转位置</summary>
    AtRight = 4,
    
    /// <summary>处于直通位置</summary>
    AtStraight = 5,
    
    /// <summary>故障状态</summary>
    Fault = 99
}
