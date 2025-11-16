using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Events;

namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 路由计划聚合根，负责管理包裹的分拣路由生命周期。
/// </summary>
public class RoutePlan
{
    private readonly List<object> _domainEvents = new();

    /// <summary>
    /// 包裹ID
    /// </summary>
    public long ParcelId { get; private set; }

    /// <summary>
    /// 初始目标格口ID
    /// </summary>
    public int InitialTargetChuteId { get; private set; }

    /// <summary>
    /// 当前有效目标格口ID
    /// </summary>
    public int CurrentTargetChuteId { get; private set; }

    /// <summary>
    /// 当前计划状态
    /// </summary>
    public RoutePlanStatus Status { get; private set; }

    /// <summary>
    /// 计划创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset LastModifiedAt { get; private set; }

    /// <summary>
    /// 最后可改口时间（可选，用于时间窗口控制）
    /// </summary>
    public DateTimeOffset? LastReplanDeadline { get; private set; }

    /// <summary>
    /// 已应用的改口次数
    /// </summary>
    public int ChuteChangeCount { get; private set; }

    /// <summary>
    /// 领域事件集合（只读）
    /// </summary>
    public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// 构造函数（用于创建新计划）
    /// </summary>
    public RoutePlan(long parcelId, int targetChuteId, DateTimeOffset createdAt, DateTimeOffset? lastReplanDeadline = null)
    {
        ParcelId = parcelId;
        InitialTargetChuteId = targetChuteId;
        CurrentTargetChuteId = targetChuteId;
        Status = RoutePlanStatus.Created;
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;
        LastReplanDeadline = lastReplanDeadline;
        ChuteChangeCount = 0;
    }

    /// <summary>
    /// 私有构造函数（用于反序列化/重建）
    /// </summary>
    private RoutePlan()
    {
    }

    /// <summary>
    /// 标记计划开始执行
    /// </summary>
    public void MarkAsExecuting(DateTimeOffset executingAt)
    {
        if (Status != RoutePlanStatus.Created)
        {
            throw new InvalidOperationException($"Cannot mark as executing from status {Status}");
        }

        Status = RoutePlanStatus.Executing;
        LastModifiedAt = executingAt;
    }

    /// <summary>
    /// 标记计划已完成
    /// </summary>
    public void MarkAsCompleted(DateTimeOffset completedAt)
    {
        if (Status == RoutePlanStatus.Completed)
        {
            return; // 已经完成，幂等操作
        }

        Status = RoutePlanStatus.Completed;
        LastModifiedAt = completedAt;
    }

    /// <summary>
    /// 标记计划已进入异常路径
    /// </summary>
    public void MarkAsExceptionRouted(DateTimeOffset routedAt)
    {
        if (Status == RoutePlanStatus.ExceptionRouted || Status == RoutePlanStatus.Completed)
        {
            return; // 已经异常或完成，幂等操作
        }

        Status = RoutePlanStatus.ExceptionRouted;
        LastModifiedAt = routedAt;
    }

    /// <summary>
    /// 标记计划失败
    /// </summary>
    public void MarkAsFailed(DateTimeOffset failedAt)
    {
        Status = RoutePlanStatus.Failed;
        LastModifiedAt = failedAt;
    }

    /// <summary>
    /// 尝试应用改口请求
    /// </summary>
    /// <param name="requestedChuteId">请求的新格口ID</param>
    /// <param name="requestedAt">请求时间</param>
    /// <param name="decision">输出决策结果</param>
    /// <returns>操作结果（成功表示已接受，失败表示被拒绝或忽略）</returns>
    public OperationResult TryApplyChuteChange(
        int requestedChuteId,
        DateTimeOffset requestedAt,
        out ChuteChangeDecision decision)
    {
        var originalChuteId = CurrentTargetChuteId;

        // 触发改口请求事件
        _domainEvents.Add(new ChuteChangeRequestedEventArgs
        {
            ParcelId = ParcelId,
            OriginalChuteId = originalChuteId,
            RequestedChuteId = requestedChuteId,
            RequestedAt = requestedAt
        });

        // 规则1: 已完成分拣，忽略改口
        if (Status == RoutePlanStatus.Completed)
        {
            decision = new ChuteChangeDecision
            {
                ParcelId = ParcelId,
                OriginalChuteId = originalChuteId,
                RequestedChuteId = requestedChuteId,
                AppliedChuteId = originalChuteId,
                Outcome = ChuteChangeOutcome.IgnoredAlreadyCompleted,
                RequestedAt = requestedAt,
                DecidedAt = requestedAt,
                Reason = "Parcel sorting already completed"
            };

            _domainEvents.Add(new ChuteChangeIgnoredEventArgs
            {
                ParcelId = ParcelId,
                RequestedChuteId = requestedChuteId,
                Outcome = ChuteChangeOutcome.IgnoredAlreadyCompleted,
                OccurredAt = requestedAt,
                Reason = decision.Reason
            });

            return OperationResult.Failure("改口被忽略：包裹已完成分拣");
        }

        // 规则2: 已进入异常路径，忽略改口
        if (Status == RoutePlanStatus.ExceptionRouted)
        {
            decision = new ChuteChangeDecision
            {
                ParcelId = ParcelId,
                OriginalChuteId = originalChuteId,
                RequestedChuteId = requestedChuteId,
                AppliedChuteId = originalChuteId,
                Outcome = ChuteChangeOutcome.IgnoredExceptionRouted,
                RequestedAt = requestedAt,
                DecidedAt = requestedAt,
                Reason = "Parcel already routed to exception chute"
            };

            _domainEvents.Add(new ChuteChangeIgnoredEventArgs
            {
                ParcelId = ParcelId,
                RequestedChuteId = requestedChuteId,
                Outcome = ChuteChangeOutcome.IgnoredExceptionRouted,
                OccurredAt = requestedAt,
                Reason = decision.Reason
            });

            return OperationResult.Failure("改口被忽略：包裹已进入异常路径");
        }

        // 规则3: 计划已失败，拒绝改口
        if (Status == RoutePlanStatus.Failed)
        {
            decision = new ChuteChangeDecision
            {
                ParcelId = ParcelId,
                OriginalChuteId = originalChuteId,
                RequestedChuteId = requestedChuteId,
                AppliedChuteId = null,
                Outcome = ChuteChangeOutcome.RejectedInvalidState,
                RequestedAt = requestedAt,
                DecidedAt = requestedAt,
                Reason = "Route plan has failed"
            };

            _domainEvents.Add(new ChuteChangeIgnoredEventArgs
            {
                ParcelId = ParcelId,
                RequestedChuteId = requestedChuteId,
                Outcome = ChuteChangeOutcome.RejectedInvalidState,
                OccurredAt = requestedAt,
                Reason = decision.Reason
            });

            return OperationResult.Failure("改口被拒绝：计划已失败");
        }

        // 规则4: 检查是否超过最后可改口时间
        if (LastReplanDeadline.HasValue && requestedAt > LastReplanDeadline.Value)
        {
            decision = new ChuteChangeDecision
            {
                ParcelId = ParcelId,
                OriginalChuteId = originalChuteId,
                RequestedChuteId = requestedChuteId,
                AppliedChuteId = null,
                Outcome = ChuteChangeOutcome.RejectedTooLate,
                RequestedAt = requestedAt,
                DecidedAt = requestedAt,
                Reason = $"Request time {requestedAt} exceeds replan deadline {LastReplanDeadline.Value}"
            };

            _domainEvents.Add(new ChuteChangeIgnoredEventArgs
            {
                ParcelId = ParcelId,
                RequestedChuteId = requestedChuteId,
                Outcome = ChuteChangeOutcome.RejectedTooLate,
                OccurredAt = requestedAt,
                Reason = decision.Reason
            });

            return OperationResult.Failure("改口被拒绝：已超过可重规划时间");
        }

        // 规则5: 接受改口（状态为 Created 或 Executing）
        CurrentTargetChuteId = requestedChuteId;
        LastModifiedAt = requestedAt;
        ChuteChangeCount++;

        decision = new ChuteChangeDecision
        {
            ParcelId = ParcelId,
            OriginalChuteId = originalChuteId,
            RequestedChuteId = requestedChuteId,
            AppliedChuteId = requestedChuteId,
            Outcome = ChuteChangeOutcome.Accepted,
            RequestedAt = requestedAt,
            DecidedAt = requestedAt,
            Reason = "Chute change accepted"
        };

        _domainEvents.Add(new ChuteChangeAcceptedEventArgs
        {
            ParcelId = ParcelId,
            OriginalChuteId = originalChuteId,
            NewChuteId = requestedChuteId,
            AcceptedAt = requestedAt
        });

        return OperationResult.Success();
    }

    /// <summary>
    /// 清除领域事件
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// 操作结果辅助类
/// </summary>
public class OperationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }

    private OperationResult(bool isSuccess, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static OperationResult Success() => new(true);
    public static OperationResult Failure(string errorMessage) => new(false, errorMessage);
}
