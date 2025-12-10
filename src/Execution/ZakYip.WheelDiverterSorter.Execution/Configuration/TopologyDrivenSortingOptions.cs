namespace ZakYip.WheelDiverterSorter.Execution.Configuration;

/// <summary>
/// 拓扑驱动分拣配置选项
/// </summary>
/// <remarks>
/// 系统现在只支持拓扑驱动的延迟执行模式：
/// ParcelCreation传感器 → 创建包裹 → 请求路由 → 计算拓扑时间 → 加入PendingQueue → 
/// WheelFront传感器触发 → 执行分拣
/// 
/// 超时时间通过线体拓扑配置自动计算：
/// 超时阈值 = (段长度mm / 线速mmps * 1000) + 容差ms
/// </remarks>
public class TopologyDrivenSortingOptions
{
    // 不再需要配置参数，所有时间都从拓扑配置计算
}
