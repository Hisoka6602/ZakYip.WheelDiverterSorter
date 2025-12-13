using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

/// <summary>
/// 落格回调触发模式
/// </summary>
/// <remarks>
/// 定义向上游发送分拣完成通知的触发时机
/// </remarks>
public enum ChuteDropoffCallbackMode
{
    /// <summary>
    /// 执行摆轮动作时触发
    /// </summary>
    /// <remarks>
    /// 在摆轮执行导向动作后立即发送分拣完成通知，
    /// 不等待包裹实际落入格口
    /// </remarks>
    [Description("执行摆轮动作时触发")]
    OnWheelExecution = 0,

    /// <summary>
    /// 落格传感器触发时触发
    /// </summary>
    /// <remarks>
    /// 等待落格传感器（ChuteDropoff）检测到包裹落入格口后，
    /// 再发送分拣完成通知
    /// </remarks>
    [Description("落格传感器触发时触发")]
    OnSensorTrigger = 1
}
