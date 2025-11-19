using Microsoft.Extensions.Logging;
using ZakYip.Sorting.Core.Contracts;
using ZakYip.WheelDiverterSorter.Execution.Pipeline.Middlewares;
using ZakYip.WheelDiverterSorter.Ingress.Upstream;

namespace ZakYip.WheelDiverterSorter.Host.Pipeline;

/// <summary>
/// 上游分配适配器，将 IUpstreamFacade 适配到 UpstreamAssignmentDelegate
/// </summary>
public class UpstreamAssignmentAdapter
{
    private readonly IUpstreamFacade _upstreamFacade;
    private readonly ILogger<UpstreamAssignmentAdapter>? _logger;

    public UpstreamAssignmentAdapter(
        IUpstreamFacade upstreamFacade,
        ILogger<UpstreamAssignmentAdapter>? logger = null)
    {
        _upstreamFacade = upstreamFacade ?? throw new ArgumentNullException(nameof(upstreamFacade));
        _logger = logger;
    }

    /// <summary>
    /// 创建适配后的委托
    /// </summary>
    public UpstreamAssignmentDelegate CreateDelegate()
    {
        return async (parcelId) =>
        {
            try
            {
                var request = new AssignChuteRequest
                {
                    ParcelId = parcelId,
                    RequestTime = DateTimeOffset.UtcNow
                };

                var result = await _upstreamFacade.AssignChuteAsync(request);

                if (result.IsSuccess && result.Data != null)
                {
                    return (
                        ChuteId: result.Data.ChuteId,
                        LatencyMs: result.LatencyMs,
                        Status: result.IsFallback ? "Fallback" : "Success",
                        Source: result.Source ?? "Unknown"
                    );
                }
                else
                {
                    _logger?.LogWarning(
                        "上游分配失败: ParcelId={ParcelId}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
                        parcelId,
                        result.ErrorCode,
                        result.ErrorMessage);

                    return (
                        ChuteId: null,
                        LatencyMs: result.LatencyMs,
                        Status: "Failed",
                        Source: result.Source ?? "Unknown"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "上游分配异常: ParcelId={ParcelId}", parcelId);
                return (
                    ChuteId: null,
                    LatencyMs: 0,
                    Status: "Exception",
                    Source: "Local"
                );
            }
        };
    }
}
