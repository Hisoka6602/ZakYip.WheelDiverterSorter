using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Pipeline;

/// <summary>
/// 分拣流水线上下文
/// </summary>
/// <remarks>
/// 包含包裹在整个分拣流程中的所有状态和数据，
/// 在各个中间件之间传递和共享。
/// </remarks>
public sealed class SortingPipelineContext
{
    /// <summary>
    /// 包裹唯一标识符
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 包裹条码（可选）
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// 包裹创建时间（UTC）
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 当前阶段名称
    /// </summary>
    /// <remarks>
    /// 典型值：Created, UpstreamAssignment, RoutePlanning, OverloadEvaluation, Execution, Completed
    /// </remarks>
    public string CurrentStage { get; set; } = "Created";

    /// <summary>
    /// 目标格口ID（由上游分配或固定模式决定）
    /// </summary>
    public long? TargetChuteId { get; set; }

    /// <summary>
    /// 实际落格的格口ID（执行完成后设置）
    /// </summary>
    public long? ActualChuteId { get; set; }

    /// <summary>
    /// 规划的摆轮路径
    /// </summary>
    public SwitchingPath? PlannedPath { get; set; }

    /// <summary>
    /// 是否应强制发送到异常口
    /// </summary>
    public bool ShouldForceException { get; set; }

    /// <summary>
    /// 异常原因（如果有）
    /// </summary>
    public string? ExceptionReason { get; set; }

    /// <summary>
    /// 异常类型
    /// </summary>
    public ExceptionType? ExceptionType { get; set; }

    /// <summary>
    /// 是否成功完成分拣
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 上游分配耗时（毫秒）
    /// </summary>
    public double UpstreamLatencyMs { get; set; }

    /// <summary>
    /// 路径规划耗时（毫秒）
    /// </summary>
    public double PlanningLatencyMs { get; set; }

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public double ExecutionLatencyMs { get; set; }

    /// <summary>
    /// 扩展数据字典，用于中间件之间传递自定义数据
    /// </summary>
    public Dictionary<string, object> Extensions { get; } = new();

    /// <summary>
    /// 获取或设置扩展数据
    /// </summary>
    public T? GetExtension<T>(string key) where T : class
    {
        return Extensions.TryGetValue(key, out var value) ? value as T : null;
    }

    /// <summary>
    /// 设置扩展数据
    /// </summary>
    public void SetExtension<T>(string key, T value) where T : class
    {
        Extensions[key] = value;
    }
}
