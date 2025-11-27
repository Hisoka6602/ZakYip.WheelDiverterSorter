using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;

namespace ZakYip.WheelDiverterSorter.Execution.Strategy;

/// <summary>
/// 固定格口选择策略
/// </summary>
/// <remarks>
/// 固定格口模式：所有包裹（异常除外）都发送到指定的固定格口。
/// 当固定格口未配置时，自动兜底到异常格口。
/// </remarks>
public class FixedChuteSelectionStrategy : IChuteSelectionStrategy
{
    private readonly ILogger<FixedChuteSelectionStrategy> _logger;

    public FixedChuteSelectionStrategy(ILogger<FixedChuteSelectionStrategy> logger)
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

        // 验证固定格口配置
        if (!context.FixedChuteId.HasValue || context.FixedChuteId.Value <= 0)
        {
            _logger.LogWarning(
                "包裹 {ParcelId} 固定格口模式配置错误：固定格口未配置或无效 (FixedChuteId={FixedChuteId})，将使用异常格口 {ExceptionChuteId}",
                context.ParcelId,
                context.FixedChuteId,
                context.ExceptionChuteId);

            return Task.FromResult(ChuteSelectionResult.Exception(
                context.ExceptionChuteId,
                "固定格口模式配置错误：固定格口未配置或无效"));
        }

        var targetChuteId = context.FixedChuteId.Value;

        _logger.LogDebug(
            "包裹 {ParcelId} 使用固定格口模式，目标格口: {ChuteId}",
            context.ParcelId,
            targetChuteId);

        return Task.FromResult(ChuteSelectionResult.Success(targetChuteId));
    }
}
