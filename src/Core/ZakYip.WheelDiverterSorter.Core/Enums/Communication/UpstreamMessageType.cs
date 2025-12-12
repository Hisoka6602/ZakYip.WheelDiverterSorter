namespace ZakYip.WheelDiverterSorter.Core.Enums.Communication;

/// <summary>
/// 上游消息类型枚举
/// </summary>
/// <remarks>
/// PR-UPSTREAM-UNIFIED: 统一上游发送接口的消息类型
/// </remarks>
public enum UpstreamMessageType
{
    /// <summary>
    /// 包裹检测通知
    /// </summary>
    ParcelDetected = 1,

    /// <summary>
    /// 落格完成通知
    /// </summary>
    SortingCompleted = 2
}
