using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Pipeline;

namespace ZakYip.WheelDiverterSorter.Execution.Pipeline;

/// <summary>
/// 分拣流水线执行器
/// </summary>
/// <remarks>
/// 按顺序执行已注册的中间件，形成完整的分拣处理流水线。
/// 类似于 ASP.NET Core 的中间件管道。
/// </remarks>
public sealed class SortingPipeline
{
    private readonly List<ISortingPipelineMiddleware> _middlewares = new();
    private readonly ILogger<SortingPipeline>? _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SortingPipeline(ILogger<SortingPipeline>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 添加中间件到流水线
    /// </summary>
    public SortingPipeline Use(ISortingPipelineMiddleware middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>
    /// 执行流水线
    /// </summary>
    public async ValueTask ExecuteAsync(SortingPipelineContext context)
    {
        if (_middlewares.Count == 0)
        {
            _logger?.LogWarning("分拣流水线为空，包裹 {ParcelId} 无法处理", context.ParcelId);
            return;
        }

        // 构建中间件链
        var index = 0;
        
        async ValueTask Next(SortingPipelineContext ctx)
        {
            if (index < _middlewares.Count)
            {
                var middleware = _middlewares[index++];
                await middleware.InvokeAsync(ctx, Next);
            }
        }

        await Next(context);
    }

    /// <summary>
    /// 获取已注册的中间件数量
    /// </summary>
    public int MiddlewareCount => _middlewares.Count;
}
