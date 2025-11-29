using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

/// <summary>
/// 摆轮命令结果类型
/// </summary>
/// <remarks>
/// 定义摆轮命令的执行结果类型。
/// 此枚举属于 Core 层的 Hardware 枚举目录。
/// </remarks>
public enum WheelCommandResultType
{
    /// <summary>
    /// 未知/解析失败
    /// </summary>
    [Description("未知")]
    Unknown = 0,

    /// <summary>
    /// 命令已应答（设备已收到命令）
    /// </summary>
    [Description("已应答")]
    Acknowledged = 1,

    /// <summary>
    /// 命令已完成（动作执行完毕）
    /// </summary>
    [Description("已完成")]
    Completed = 2,

    /// <summary>
    /// 命令执行失败
    /// </summary>
    [Description("执行失败")]
    Failed = 3
}
