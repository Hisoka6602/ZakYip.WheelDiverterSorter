namespace ZakYip.Sorting.Core.Pipeline;

/// <summary>
/// 分拣流水线中间件接口
/// </summary>
/// <remarks>
/// 每个中间件负责处理分拣流程中的一个关键步骤，并可以：
/// - 检查和修改上下文
/// - 决定是否继续执行后续中间件
/// - 发布事件通知
/// - 记录追踪日志
/// </remarks>
public interface ISortingPipelineMiddleware
{
    /// <summary>
    /// 执行中间件逻辑
    /// </summary>
    /// <param name="context">分拣流水线上下文</param>
    /// <param name="next">下一个中间件委托</param>
    /// <returns>异步任务</returns>
    /// <remarks>
    /// 实现应遵循以下模式：
    /// 1. 执行前置逻辑（检查、修改上下文）
    /// 2. 调用 await next(context) 执行后续中间件
    /// 3. 执行后置逻辑（清理、记录）
    /// 
    /// 如果需要短路流水线（例如异常处理），可以不调用 next 直接返回。
    /// </remarks>
    ValueTask InvokeAsync(SortingPipelineContext context, Func<SortingPipelineContext, ValueTask> next);
}
