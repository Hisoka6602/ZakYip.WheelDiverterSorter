using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;

namespace ZakYip.WheelDiverterSorter.Execution.Abstractions;

/// <summary>
/// 上游通讯契约映射器接口
/// </summary>
/// <remarks>
/// <para><b>职责</b>：在领域层对象与上游通讯 DTO 之间进行转换</para>
/// <list type="bullet">
///   <item>将领域层的 <see cref="SortingRequest"/> 转换为上游协议的请求 DTO</item>
///   <item>将上游协议的响应 DTO 转换为领域层的 <see cref="SortingResponse"/></item>
///   <item>处理不同上游协议（HTTP/TCP/SignalR/MQTT）的契约差异</item>
/// </list>
/// <para><b>设计原则</b>：</para>
/// <para>
/// 此接口定义在 Execution.Abstractions 中，不依赖具体协议实现。
/// 具体的契约映射实现在 Communication 层实现，
/// 确保上游协议细节不渗透到领域层。
/// </para>
/// <para><b>与 ParcelDescriptor 的关系</b>：</para>
/// <para>
/// <see cref="SortingRequest"/> 是边界层契约，可通过
/// <see cref="ParcelDescriptorExtensions.ToParcelDescriptor(SortingRequest)"/>
/// 转换为统一领域模型 <see cref="ParcelDescriptor"/>。
/// </para>
/// <para><b>使用示例</b>：</para>
/// <code>
/// // 在网关层使用映射器
/// var protocolRequest = _mapper.MapToUpstreamRequest(sortingRequest);
/// var protocolResponse = await _client.SendAsync(protocolRequest);
/// var sortingResponse = _mapper.MapFromUpstreamResponse(sortingRequest.ParcelId, protocolResponse);
/// </code>
/// </remarks>
public interface IUpstreamContractMapper
{
    /// <summary>
    /// 契约映射器的协议标识
    /// </summary>
    /// <remarks>
    /// 用于标识此映射器所适配的上游协议类型，如 HTTP、TCP、SignalR 等
    /// </remarks>
    string ProtocolName { get; }

    /// <summary>
    /// 将领域层分拣请求映射为上游协议请求
    /// </summary>
    /// <param name="request">领域层分拣请求</param>
    /// <returns>上游协议请求对象</returns>
    /// <remarks>
    /// 实现类应根据具体协议格式构造请求对象。
    /// 例如，HTTP 协议可能需要构造 JSON 请求体，TCP 协议可能需要构造二进制帧。
    /// </remarks>
    UpstreamSortingRequest MapToUpstreamRequest(SortingRequest request);

    /// <summary>
    /// 将上游协议响应映射为领域层分拣响应
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="response">上游协议响应对象</param>
    /// <returns>领域层分拣响应</returns>
    /// <remarks>
    /// 实现类应将协议响应中的状态码、错误信息等转换为领域层可理解的格式。
    /// 隐藏协议细节，如 HTTP 状态码、TCP 应答码等。
    /// </remarks>
    SortingResponse MapFromUpstreamResponse(long parcelId, UpstreamSortingResponse response);

    /// <summary>
    /// 将上游推送的格口分配映射为领域层分拣响应
    /// </summary>
    /// <param name="notification">上游推送的格口分配通知</param>
    /// <returns>领域层分拣响应</returns>
    /// <remarks>
    /// 用于处理上游系统主动推送的格口分配结果（如通过 WebSocket/SignalR 推送）。
    /// </remarks>
    SortingResponse MapFromUpstreamNotification(UpstreamChuteAssignmentNotification notification);

    /// <summary>
    /// 创建上游请求失败时的降级响应
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="fallbackChuteId">降级格口ID</param>
    /// <param name="reasonCode">失败原因代码</param>
    /// <param name="errorMessage">错误消息</param>
    /// <returns>降级的领域层分拣响应</returns>
    /// <remarks>
    /// 当上游通讯失败时，使用此方法创建一个标记为异常路由的响应。
    /// </remarks>
    SortingResponse CreateFallbackResponse(
        long parcelId,
        long fallbackChuteId,
        string reasonCode,
        string? errorMessage);
}

/// <summary>
/// 上游协议分拣请求（通讯层模型）
/// </summary>
/// <remarks>
/// <para>
/// 与具体上游协议解耦的通用请求模型，包含所有协议可能需要的字段。
/// 具体协议实现可选择使用其中的字段。
/// </para>
/// <para><b>与领域模型的关系</b>：</para>
/// <para>
/// 此类型是通讯层的边界模型，其包裹基础信息与领域模型 <see cref="ParcelDescriptor"/>
/// 存在字段重叠。设计上，此模型用于上游通讯协议，而 <see cref="ParcelDescriptor"/>
/// 用于领域层内部流转。
/// </para>
/// </remarks>
public record UpstreamSortingRequest
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 包裹条码（可选）
    /// </summary>
    public string? Barcode { get; init; }

    /// <summary>
    /// 请求时间
    /// </summary>
    public required DateTimeOffset RequestTime { get; init; }

    /// <summary>
    /// 检测传感器ID（可选）
    /// </summary>
    public string? SensorId { get; init; }

    /// <summary>
    /// 候选格口ID列表（可选）
    /// </summary>
    public IReadOnlyList<int>? CandidateChuteIds { get; init; }

    /// <summary>
    /// 附加元数据（可选）
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// 上游协议分拣响应（通讯层模型）
/// </summary>
/// <remarks>
/// 与具体上游协议解耦的通用响应模型。
/// </remarks>
public record UpstreamSortingResponse
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
    /// 协议状态码（原始值）
    /// </summary>
    /// <remarks>
    /// 保留原始协议状态码，供日志记录和调试使用。
    /// 业务逻辑不应直接使用此字段。
    /// </remarks>
    public int? ProtocolStatusCode { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 响应时间
    /// </summary>
    public required DateTimeOffset ResponseTime { get; init; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// 上游格口分配推送通知（通讯层模型）
/// </summary>
/// <remarks>
/// 用于表示上游系统主动推送的格口分配结果。
/// </remarks>
public record UpstreamChuteAssignmentNotification
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 分配的格口ID
    /// </summary>
    public required long ChuteId { get; init; }

    /// <summary>
    /// 通知时间
    /// </summary>
    public required DateTimeOffset NotificationTime { get; init; }

    /// <summary>
    /// 来源标识
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// 附加元数据
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
