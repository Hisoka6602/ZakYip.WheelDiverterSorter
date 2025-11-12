namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 已知的格口标识符常量
/// </summary>
/// <remarks>
/// 定义系统中使用的标准格口ID，避免魔法字符串
/// </remarks>
public static class WellKnownChuteIds
{
    /// <summary>
    /// 异常格口ID - 用于处理分拣失败或无法分配格口的包裹
    /// </summary>
    public const string Exception = "CHUTE_EXCEPTION";
}
