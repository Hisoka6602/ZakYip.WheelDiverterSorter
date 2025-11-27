using ZakYip.WheelDiverterSorter.Core.Sorting.Models;

namespace ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;

/// <summary>
/// 分配格口请求
/// </summary>
/// <remarks>
/// <para>向上游规则引擎请求为包裹分配目标格口。</para>
/// <para>
/// 此为边界层契约类型，在进入业务逻辑后应通过
/// <see cref="ParcelDescriptorExtensions.ToParcelDescriptor(AssignChuteRequest)"/>
/// 转换为统一领域模型 <see cref="ParcelDescriptor"/>。
/// </para>
/// </remarks>
public record AssignChuteRequest
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 包裹条码
    /// </summary>
    public string? Barcode { get; init; }

    /// <summary>
    /// 请求时间
    /// </summary>
    public DateTimeOffset RequestTime { get; init; }

    /// <summary>
    /// 候选格口ID列表
    /// </summary>
    /// <remarks>
    /// 可选字段，用于提示上游系统可用的格口列表
    /// </remarks>
    public IReadOnlyList<int>? CandidateChuteIds { get; init; }

    /// <summary>
    /// 检测传感器ID
    /// </summary>
    public string? SensorId { get; init; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// 分配格口响应
/// </summary>
/// <remarks>
/// 上游规则引擎返回的格口分配结果
/// </remarks>
public record AssignChuteResponse
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public required long ChuteId { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// 响应时间
    /// </summary>
    public DateTimeOffset ResponseTime { get; init; }

    /// <summary>
    /// 来源（例如：RuleEngine、LocalDecision、FallbackPolicy）
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// 原因代码
    /// </summary>
    public string? ReasonCode { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
