using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;

namespace ZakYip.WheelDiverterSorter.Execution.Strategy;

/// <summary>
/// 组合格口选择服务
/// </summary>
/// <remarks>
/// 根据当前分拣模式自动路由到对应的策略实现。
/// 这是统一的入口点，Host / Worker / Orchestrator 只依赖此服务。
/// </remarks>
public class CompositeChuteSelectionService : IChuteSelectionService
{
    private readonly FixedChuteSelectionStrategy _fixedStrategy;
    private readonly RoundRobinChuteSelectionStrategy _roundRobinStrategy;
    private readonly FormalChuteSelectionStrategy _formalStrategy;
    private readonly ILogger<CompositeChuteSelectionService> _logger;

    public CompositeChuteSelectionService(
        FixedChuteSelectionStrategy fixedStrategy,
        RoundRobinChuteSelectionStrategy roundRobinStrategy,
        FormalChuteSelectionStrategy formalStrategy,
        ILogger<CompositeChuteSelectionService> logger)
    {
        _fixedStrategy = fixedStrategy ?? throw new ArgumentNullException(nameof(fixedStrategy));
        _roundRobinStrategy = roundRobinStrategy ?? throw new ArgumentNullException(nameof(roundRobinStrategy));
        _formalStrategy = formalStrategy ?? throw new ArgumentNullException(nameof(formalStrategy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ChuteSelectionResult> SelectChuteAsync(SortingContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "包裹 {ParcelId} 开始格口选择，分拣模式: {SortingMode}",
            context.ParcelId,
            context.SortingMode);

        var strategy = GetStrategy(context.SortingMode);
        var result = await strategy.SelectChuteAsync(context, cancellationToken);

        if (result.IsSuccess)
        {
            if (result.IsException)
            {
                _logger.LogInformation(
                    "包裹 {ParcelId} 格口选择完成，路由到异常格口 {ChuteId}。原因: {Reason}",
                    context.ParcelId,
                    result.TargetChuteId,
                    result.ExceptionReason);
            }
            else
            {
                _logger.LogDebug(
                    "包裹 {ParcelId} 格口选择完成，目标格口: {ChuteId}",
                    context.ParcelId,
                    result.TargetChuteId);
            }
        }
        else
        {
            _logger.LogError(
                "包裹 {ParcelId} 格口选择失败: {ErrorMessage}",
                context.ParcelId,
                result.ErrorMessage);
        }

        return result;
    }

    /// <summary>
    /// 根据分拣模式获取对应的策略
    /// </summary>
    private IChuteSelectionStrategy GetStrategy(SortingMode sortingMode)
    {
        return sortingMode switch
        {
            SortingMode.Formal => _formalStrategy,
            SortingMode.FixedChute => _fixedStrategy,
            SortingMode.RoundRobin => _roundRobinStrategy,
            _ => throw new ArgumentOutOfRangeException(nameof(sortingMode), sortingMode, $"未知的分拣模式: {sortingMode}")
        };
    }
}
