namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 已知的格口标识符常量
/// </summary>
/// <remarks>
/// 定义系统中使用的标准格口ID，避免魔法字符串
/// 注意：异常格口ID现在从系统配置中读取，不再使用硬编码常量
/// </remarks>
public static class WellKnownChuteIds
{
    /// <summary>
    /// 默认异常格口ID - 用于处理分拣失败或无法分配格口的包裹
    /// </summary>
    /// <remarks>
    /// 此值为默认值，实际使用时应从系统配置中读取
    /// </remarks>
    public const string DefaultException = "CHUTE_EXCEPTION";
}
