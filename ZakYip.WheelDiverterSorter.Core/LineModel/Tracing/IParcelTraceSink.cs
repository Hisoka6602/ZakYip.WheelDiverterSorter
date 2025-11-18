namespace ZakYip.WheelDiverterSorter.Core.Tracing;

/// <summary>
/// 单件分拣审计日志写入接口
/// </summary>
/// <remarks>
/// <para>该接口用于审计与排障，记录包裹在分拣过程中的关键生命周期事件。</para>
/// <para>后端采用追加式日志文件，数据保留期有限（通常 7-14 天）。</para>
/// <para>写入失败不得影响主业务流程 - 实现必须捕获所有异常。</para>
/// </remarks>
public interface IParcelTraceSink
{
    /// <summary>
    /// 写入一条分拣轨迹事件
    /// </summary>
    /// <param name="eventArgs">事件参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    /// <remarks>
    /// 实现必须：
    /// - 捕获所有异常，不向调用方抛出
    /// - 异常时仅记录内部日志（Warning 级别）
    /// - 快速返回，避免阻塞主业务流程
    /// </remarks>
    ValueTask WriteAsync(ParcelTraceEventArgs eventArgs, CancellationToken cancellationToken = default);
}
