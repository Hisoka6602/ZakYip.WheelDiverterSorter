namespace ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;

/// <summary>
/// 分拣编排服务接口
/// </summary>
/// <remarks>
/// 负责协调整个分拣流程的核心业务逻辑，是分拣主流程的统一入口。
/// 
/// <para><b>完整流程</b>：</para>
/// <list type="number">
///   <item>包裹感应触发 → 创建本地包裹实体（Parcel-First）</item>
///   <item>验证系统状态和包裹创建条件</item>
///   <item>拥堵检测与超载处置评估</item>
///   <item>确定目标格口（上游路由 / 固定格口 / 轮询）</item>
///   <item>生成摆轮切换路径</item>
///   <item>路径健康检查与二次超载检查</item>
///   <item>执行摆轮切换序列</item>
///   <item>记录分拣结果和追踪日志</item>
/// </list>
/// 
/// <para><b>设计原则</b>：</para>
/// <list type="bullet">
///   <item>单一职责：每个方法只负责一个步骤</item>
///   <item>可测试性：方法小而清晰，易于单元测试</item>
///   <item>PR-42 Parcel-First：确保本地包裹先创建，再请求上游</item>
///   <item>异常处理：所有异常场景路由到异常格口</item>
/// </list>
/// </remarks>
public interface ISortingOrchestrator
{
    /// <summary>
    /// 启动编排服务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>启动任务</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止编排服务
    /// </summary>
    /// <returns>停止任务</returns>
    Task StopAsync();

    /// <summary>
    /// 处理包裹分拣流程
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="sensorId">触发传感器ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分拣结果</returns>
    /// <remarks>
    /// 这是分拣主流程的统一入口。
    /// 
    /// <para><b>调用来源</b>：</para>
    /// <list type="bullet">
    ///   <item>真实传感器触发：IParcelDetectionService.ParcelDetected 事件</item>
    ///   <item>仿真场景：SimulationScenarioRunner 模拟传感器触发</item>
    ///   <item>手动触发：DebugSortService 调用（测试环境）</item>
    /// </list>
    /// 
    /// <para><b>流程步骤</b>：</para>
    /// <list type="number">
    ///   <item>创建包裹实体（PR-42 Parcel-First）</item>
    ///   <item>验证系统状态（必须为 Running）</item>
    ///   <item>拥堵检测与超载评估</item>
    ///   <item>确定目标格口（上游/固定/轮询）</item>
    ///   <item>生成和执行摆轮切换路径</item>
    ///   <item>记录结果和追踪日志</item>
    /// </list>
    /// </remarks>
    Task<SortingResult> ProcessParcelAsync(long parcelId, string sensorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行调试分拣（跳过包裹创建和上游路由）
    /// </summary>
    /// <param name="parcelId">包裹ID（用于日志和追踪）</param>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分拣结果</returns>
    /// <remarks>
    /// 仅供测试和仿真环境使用。
    /// 直接执行路径生成和执行，不经过包裹创建和上游路由流程。
    /// 
    /// <para><b>使用场景</b>：</para>
    /// <list type="bullet">
    ///   <item>手动触发分拣测试</item>
    ///   <item>路径验证测试</item>
    ///   <item>硬件驱动测试</item>
    /// </list>
    /// </remarks>
    Task<SortingResult> ExecuteDebugSortAsync(string parcelId, long targetChuteId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 分拣结果
/// </summary>
/// <param name="IsSuccess">是否成功</param>
/// <param name="ParcelId">包裹ID</param>
/// <param name="ActualChuteId">实际到达的格口ID</param>
/// <param name="TargetChuteId">目标格口ID（可能与实际不同，如果失败）</param>
/// <param name="ExecutionTimeMs">执行时间（毫秒）</param>
/// <param name="FailureReason">失败原因（如果失败）</param>
/// <param name="IsOverloadException">是否因超载路由到异常格口</param>
public record SortingResult(
    bool IsSuccess,
    string ParcelId,
    long ActualChuteId,
    long TargetChuteId,
    double ExecutionTimeMs,
    string? FailureReason = null,
    bool IsOverloadException = false
);
