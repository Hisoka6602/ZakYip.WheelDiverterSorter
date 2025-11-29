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
    Unknown = 0,

    /// <summary>
    /// 命令已应答（设备已收到命令）
    /// </summary>
    Acknowledged = 1,

    /// <summary>
    /// 命令已完成（动作执行完毕）
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 命令执行失败
    /// </summary>
    Failed = 3
}
