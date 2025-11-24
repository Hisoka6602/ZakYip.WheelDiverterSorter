using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Routing;
using ZakYip.WheelDiverterSorter.Execution;

namespace ZakYip.WheelDiverterSorter.Host.Commands;

/// <summary>
/// 改口命令处理器
/// </summary>
public class ChangeParcelChuteCommandHandler
{
    private readonly IRoutePlanRepository _routePlanRepository;
    private readonly IRouteReplanner _routeReplanner;
    private readonly ILogger<ChangeParcelChuteCommandHandler> _logger;

    public ChangeParcelChuteCommandHandler(
        IRoutePlanRepository routePlanRepository,
        IRouteReplanner routeReplanner,
        ILogger<ChangeParcelChuteCommandHandler> logger)
    {
        _routePlanRepository = routePlanRepository ?? throw new ArgumentNullException(nameof(routePlanRepository));
        _routeReplanner = routeReplanner ?? throw new ArgumentNullException(nameof(routeReplanner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 处理改口命令
    /// </summary>
    public async Task<ChangeParcelChuteResult> HandleAsync(
        ChangeParcelChuteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var requestedAt = command.RequestedAt ?? DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Processing chute change request for parcel {ParcelId} to chute {ChuteId}",
            command.ParcelId, command.RequestedChuteId);

        // 1. 加载路由计划
        var routePlan = await _routePlanRepository.GetByParcelIdAsync(command.ParcelId, cancellationToken);

        if (routePlan == null)
        {
            _logger.LogWarning(
                "Route plan not found for parcel {ParcelId}",
                command.ParcelId);

            return ChangeParcelChuteResult.Failure(
                command.ParcelId,
                command.RequestedChuteId,
                $"Route plan not found for parcel {command.ParcelId}");
        }

        // 2. 调用领域方法尝试改口
        var result = routePlan.TryApplyChuteChange(
            command.RequestedChuteId,
            requestedAt,
            out var decision);

        var originalChuteId = decision.OriginalChuteId;

        // 3. 根据决策结果处理
        if (result.IsSuccess && decision.Outcome == ChuteChangeOutcome.Accepted)
        {
            _logger.LogInformation(
                "Chute change accepted for parcel {ParcelId}: {OriginalChute} -> {NewChute}",
                command.ParcelId, originalChuteId, command.RequestedChuteId);

            // 4. 调用Execution层重规划路径
            var replanResult = await _routeReplanner.ReplanAsync(
                command.ParcelId,
                command.RequestedChuteId,
                requestedAt,
                cancellationToken);

            if (!replanResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Replan failed for parcel {ParcelId}: {Reason}",
                    command.ParcelId, replanResult.FailureReason);

                // 重规划失败，但领域层已接受改口，这是不一致状态
                // 实际应用中可能需要回滚或进入异常处理流程
                // 这里简化处理：标记为异常路由
                routePlan.MarkAsExceptionRouted(DateTimeOffset.UtcNow);
            }

            // 5. 保存更新后的路由计划
            await _routePlanRepository.SaveAsync(routePlan, cancellationToken);

            return ChangeParcelChuteResult.Success(
                command.ParcelId,
                originalChuteId,
                command.RequestedChuteId,
                decision.AppliedChuteId ?? command.RequestedChuteId,
                decision.Outcome,
                replanResult.IsSuccess
                    ? "Chute change accepted and path replanned successfully"
                    : $"Chute change accepted but replan failed: {replanResult.FailureReason}");
        }
        else
        {
            // 改口被忽略或拒绝
            _logger.LogInformation(
                "Chute change ignored/rejected for parcel {ParcelId}: Outcome={Outcome}, Reason={Reason}",
                command.ParcelId, decision.Outcome, decision.Reason);

            // 保存更新后的路由计划（记录了领域事件）
            await _routePlanRepository.SaveAsync(routePlan, cancellationToken);

            return ChangeParcelChuteResult.Success(
                command.ParcelId,
                originalChuteId,
                command.RequestedChuteId,
                decision.AppliedChuteId ?? originalChuteId,
                decision.Outcome,
                decision.Reason);
        }
    }
}
