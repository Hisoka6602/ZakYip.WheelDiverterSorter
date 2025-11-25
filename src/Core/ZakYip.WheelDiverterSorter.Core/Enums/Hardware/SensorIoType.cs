using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 感应IO类型 - 按业务功能分类
/// </summary>
/// <remarks>
/// 感应IO按业务功能分为三种类型：
/// - ParcelCreation: 创建包裹感应IO，用于感应包裹进入系统并创建包裹实体
/// - WheelFront: 摆轮前感应IO，用于检测包裹即将到达摆轮
/// - ChuteLock: 锁格感应IO，用于检测包裹落入格口
/// 
/// 注意：
/// - 一个系统中只能有一个 ParcelCreation 类型的感应IO处于激活状态
/// - WheelFront 类型的感应IO与摆轮的 frontIoId 关联
/// </remarks>
public enum SensorIoType
{
    /// <summary>
    /// 创建包裹感应IO - 感应包裹进入系统并触发包裹创建
    /// </summary>
    /// <remarks>
    /// 只能同时存在/生效一个创建包裹感应IO
    /// </remarks>
    [Description("创建包裹感应IO")]
    ParcelCreation = 0,

    /// <summary>
    /// 摆轮前感应IO - 检测包裹即将到达摆轮
    /// </summary>
    /// <remarks>
    /// 与摆轮的 frontIoId 关联，用于触发摆轮提前动作
    /// </remarks>
    [Description("摆轮前感应IO")]
    WheelFront = 1,

    /// <summary>
    /// 锁格感应IO - 检测包裹落入格口
    /// </summary>
    /// <remarks>
    /// 用于确认包裹已成功分拣到目标格口
    /// </remarks>
    [Description("锁格感应IO")]
    ChuteLock = 2
}
