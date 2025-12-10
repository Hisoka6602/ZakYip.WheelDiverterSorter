using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 摆轮控制命令类型
/// </summary>
public enum WheelDiverterCommand
{
    /// <summary>
    /// 运行
    /// </summary>
    [Description("运行")]
    Run = 0,

    /// <summary>
    /// 停止
    /// </summary>
    [Description("停止")]
    Stop = 1
}
