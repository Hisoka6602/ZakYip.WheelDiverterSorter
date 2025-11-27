using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;

namespace ZakYip.WheelDiverterSorter.Execution.Strategy;

/// <summary>
/// 轮询格口选择策略
/// </summary>
/// <remarks>
/// 轮询模式：按顺序循环分配格口。
/// 第一个包裹落格口1，第二个包裹落格口2，依此类推，循环往复。
/// 当可用格口列表未配置或为空时，自动兜底到异常格口。
/// </remarks>
public class RoundRobinChuteSelectionStrategy : IChuteSelectionStrategy
{
    private readonly ILogger<RoundRobinChuteSelectionStrategy> _logger;
    private readonly object _lockObject = new();
    private int _roundRobinIndex;

    public RoundRobinChuteSelectionStrategy(ILogger<RoundRobinChuteSelectionStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<ChuteSelectionResult> SelectChuteAsync(SortingContext context, CancellationToken cancellationToken)
    {
        // 如果因超载强制路由到异常格口
        if (context.IsOverloadForced)
        {
            _logger.LogDebug(
                "包裹 {ParcelId} 因超载强制路由到异常格口 {ExceptionChuteId}",
                context.ParcelId,
                context.ExceptionChuteId);

            return Task.FromResult(ChuteSelectionResult.Exception(
                context.ExceptionChuteId,
                "超载强制路由到异常格口"));
        }

        // 验证可用格口列表配置
        if (context.AvailableChuteIds == null || context.AvailableChuteIds.Count == 0)
        {
            _logger.LogWarning(
                "包裹 {ParcelId} 循环格口落格模式配置错误：没有可用格口，将使用异常格口 {ExceptionChuteId}",
                context.ParcelId,
                context.ExceptionChuteId);

            return Task.FromResult(ChuteSelectionResult.Exception(
                context.ExceptionChuteId,
                "循环格口落格模式配置错误：没有可用格口"));
        }

        long targetChuteId;

        // 使用锁保证轮询索引的线程安全
        lock (_lockObject)
        {
            // 确保索引在有效范围内（防止配置变更导致索引越界）
            if (_roundRobinIndex >= context.AvailableChuteIds.Count)
            {
                _roundRobinIndex = 0;
            }

            targetChuteId = context.AvailableChuteIds[_roundRobinIndex];
            _roundRobinIndex = (_roundRobinIndex + 1) % context.AvailableChuteIds.Count;
        }

        _logger.LogDebug(
            "包裹 {ParcelId} 使用轮询模式，目标格口: {ChuteId}",
            context.ParcelId,
            targetChuteId);

        return Task.FromResult(ChuteSelectionResult.Success(targetChuteId));
    }

    /// <summary>
    /// 重置轮询索引（用于测试或配置变更时）
    /// </summary>
    public void ResetIndex()
    {
        lock (_lockObject)
        {
            _roundRobinIndex = 0;
        }
    }

    /// <summary>
    /// 获取当前轮询索引（仅用于测试）
    /// </summary>
    internal int CurrentIndex
    {
        get
        {
            lock (_lockObject)
            {
                return _roundRobinIndex;
            }
        }
    }
}
