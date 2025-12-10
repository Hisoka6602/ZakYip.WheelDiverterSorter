namespace ZakYip.WheelDiverterSorter.Execution.Configuration;

/// <summary>
/// 拓扑驱动分拣配置选项
/// </summary>
/// <remarks>
/// 系统现在只支持拓扑驱动的延迟执行模式：
/// ParcelCreation传感器 → 创建包裹 → 请求路由 → 加入PendingQueue → 
/// WheelFront传感器触发 → 执行分拣
/// </remarks>
public class TopologyDrivenSortingOptions
{
    /// <summary>
    /// 默认超时时间（秒）
    /// </summary>
    /// <remarks>
    /// 包裹在PendingQueue中等待超过此时间将被视为超时，
    /// 自动路由到异常格口。
    /// 默认值: 30秒
    /// </remarks>
    public int DefaultTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 超时监控检查间隔（秒）
    /// </summary>
    /// <remarks>
    /// 后台监控服务每隔此时间检查一次PendingQueue中是否有超时包裹。
    /// 默认值: 10秒
    /// </remarks>
    public int MonitorIntervalSeconds { get; set; } = 10;
}
