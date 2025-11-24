using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

/// <summary>
/// 高密度包裹处理策略
/// </summary>
/// <remarks>
/// 定义当检测到包裹间隔过近（违反最小安全头距）时的处理方式
/// </remarks>
public enum DenseParcelStrategy
{
    /// <summary>
    /// 默认策略：将间隔过近的包裹路由到异常格口
    /// </summary>
    /// <remarks>
    /// 包裹会被标记为执行错误，并路由到配置的异常格口
    /// </remarks>
    [Description("路由到异常")]
    RouteToException,

    /// <summary>
    /// 将间隔过近的包裹标记为 Timeout，视为未完成分拣
    /// </summary>
    /// <remarks>
    /// 包裹会被标记为超时状态，不生成正常分拣结果
    /// </remarks>
    [Description("标记为超时")]
    MarkAsTimeout,

    /// <summary>
    /// 将间隔过近的包裹标记为 Dropped，视为在系统中丢失
    /// </summary>
    /// <remarks>
    /// 包裹会被标记为掉包状态，模拟在传输过程中丢失
    /// </remarks>
    [Description("标记为掉落")]
    MarkAsDropped
}
