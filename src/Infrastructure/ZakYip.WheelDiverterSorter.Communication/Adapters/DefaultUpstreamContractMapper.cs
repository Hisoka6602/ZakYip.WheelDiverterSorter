using ZakYip.WheelDiverterSorter.Core.Sorting.Contracts;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;

namespace ZakYip.WheelDiverterSorter.Communication.Adapters;

/// <summary>
/// 默认上游契约映射器实现
/// </summary>
/// <remarks>
/// <para>负责在领域层分拣对象与上游通讯 DTO 之间进行转换。</para>
/// <para>此实现适用于通用的 JSON/HTTP 协议，其他协议（如二进制 TCP）可创建专用映射器。</para>
/// </remarks>
public sealed class DefaultUpstreamContractMapper : IUpstreamContractMapper
{
    /// <inheritdoc/>
    public string ProtocolName => "Default";

    /// <inheritdoc/>
    public UpstreamSortingRequest MapToUpstreamRequest(SortingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpstreamSortingRequest
        {
            ParcelId = request.ParcelId,
            Barcode = request.Barcode,
            RequestTime = request.RequestTime,
            SensorId = request.SensorId,
            CandidateChuteIds = request.CandidateChuteIds,
            Metadata = request.Metadata
        };
    }

    /// <inheritdoc/>
    public SortingResponse MapFromUpstreamResponse(long parcelId, UpstreamSortingResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new SortingResponse
        {
            ParcelId = parcelId,
            TargetChuteId = response.ChuteId,
            IsSuccess = response.IsSuccess,
            IsException = !response.IsSuccess,
            ReasonCode = response.IsSuccess ? "SUCCESS" : "UPSTREAM_FAILED",
            ErrorMessage = response.ErrorMessage,
            ResponseTime = response.ResponseTime,
            Source = ProtocolName,
            Metadata = response.Metadata
        };
    }

    /// <inheritdoc/>
    public SortingResponse MapFromUpstreamNotification(UpstreamChuteAssignmentNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return new SortingResponse
        {
            ParcelId = notification.ParcelId,
            TargetChuteId = notification.ChuteId,
            IsSuccess = true,
            IsException = false,
            ReasonCode = "SUCCESS",
            ResponseTime = notification.NotificationTime,
            Source = notification.Source ?? ProtocolName,
            Metadata = notification.Metadata
        };
    }

    /// <inheritdoc/>
    public SortingResponse CreateFallbackResponse(
        long parcelId,
        long fallbackChuteId,
        string reasonCode,
        string? errorMessage)
    {
        return new SortingResponse
        {
            ParcelId = parcelId,
            TargetChuteId = fallbackChuteId,
            IsSuccess = true, // 降级也是一种"成功"处理
            IsException = true,
            ReasonCode = reasonCode,
            ErrorMessage = errorMessage,
            ResponseTime = DateTimeOffset.Now,
            Source = "Fallback"
        };
    }
}
