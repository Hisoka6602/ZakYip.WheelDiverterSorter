namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 路径时间预估结果
/// </summary>
public record RouteTimingEstimate
{
    /// <summary>
    /// 格口ID
    /// </summary>
    public required string ChuteId { get; init; }

    /// <summary>
    /// 路径总距离（毫米）
    /// </summary>
    public required double TotalDistanceMm { get; init; }

    /// <summary>
    /// 预计到达时间（毫秒）
    /// </summary>
    public required double EstimatedArrivalTimeMs { get; init; }

    /// <summary>
    /// 使用的线速（毫米/秒）
    /// </summary>
    public required double SpeedMmPerSec { get; init; }

    /// <summary>
    /// 路径上的线体段数量
    /// </summary>
    public required int SegmentCount { get; init; }

    /// <summary>
    /// 是否成功计算
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 错误消息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 路径时间预估服务接口
/// </summary>
public interface IRouteTimingEstimator
{
    /// <summary>
    /// 估算包裹到达指定格口的时间
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <param name="speedMmPerSec">线速（毫米/秒），如果为null则使用拓扑配置中的标称速度</param>
    /// <returns>时间预估结果</returns>
    RouteTimingEstimate EstimateArrivalTime(string chuteId, double? speedMmPerSec = null);

    /// <summary>
    /// 计算超时阈值（毫秒）
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <param name="toleranceFactor">容忍系数（默认1.5，即理论时间的150%）</param>
    /// <param name="speedMmPerSec">线速（毫米/秒），如果为null则使用拓扑配置中的标称速度</param>
    /// <returns>超时阈值（毫秒），如果计算失败则返回null</returns>
    double? CalculateTimeoutThreshold(string chuteId, double toleranceFactor = 1.5, double? speedMmPerSec = null);
}

/// <summary>
/// 路径时间预估服务实现
/// </summary>
public class RouteTimingEstimator : IRouteTimingEstimator
{
    private readonly ILineTopologyRepository _topologyRepository;

    public RouteTimingEstimator(ILineTopologyRepository topologyRepository)
    {
        _topologyRepository = topologyRepository ?? throw new ArgumentNullException(nameof(topologyRepository));
    }

    /// <summary>
    /// 估算包裹到达指定格口的时间
    /// </summary>
    /// <remarks>
    /// 使用新的IO模型：通过线体段的起点IO和终点IO计算路径时间
    /// </remarks>
    public RouteTimingEstimate EstimateArrivalTime(string chuteId, double? speedMmPerSec = null)
    {
        if (string.IsNullOrWhiteSpace(chuteId))
        {
            return new RouteTimingEstimate
            {
                ChuteId = chuteId ?? string.Empty,
                TotalDistanceMm = 0,
                EstimatedArrivalTimeMs = 0,
                SpeedMmPerSec = 0,
                SegmentCount = 0,
                IsSuccess = false,
                ErrorMessage = "格口ID不能为空 - Chute ID cannot be empty"
            };
        }

        var topology = _topologyRepository.Get();
        var chute = topology.FindChuteById(chuteId);
        
        if (chute == null)
        {
            return new RouteTimingEstimate
            {
                ChuteId = chuteId,
                TotalDistanceMm = 0,
                EstimatedArrivalTimeMs = 0,
                SpeedMmPerSec = speedMmPerSec ?? (double)topology.DefaultLineSpeedMmps,
                SegmentCount = 0,
                IsSuccess = false,
                ErrorMessage = $"无法找到格口 {chuteId} - Cannot find chute {chuteId}"
            };
        }

        // 使用线体段计算距离和时间
        if (topology.LineSegments.Count == 0)
        {
            return new RouteTimingEstimate
            {
                ChuteId = chuteId,
                TotalDistanceMm = 0,
                EstimatedArrivalTimeMs = 0,
                SpeedMmPerSec = speedMmPerSec ?? (double)topology.DefaultLineSpeedMmps,
                SegmentCount = 0,
                IsSuccess = false,
                ErrorMessage = "未配置线体段 - No line segments configured"
            };
        }

        var dropOffsetMm = chute.DropOffsetMm;

        // 计算所有线体段的时间和距离
        double totalTimeMs = 0;
        double totalDistanceMm = dropOffsetMm;

        foreach (var segment in topology.LineSegments)
        {
            var segmentSpeed = speedMmPerSec ?? segment.SpeedMmPerSec;
            if (segmentSpeed <= 0)
            {
                return new RouteTimingEstimate
                {
                    ChuteId = chuteId,
                    TotalDistanceMm = 0,
                    EstimatedArrivalTimeMs = 0,
                    SpeedMmPerSec = segmentSpeed,
                    SegmentCount = topology.LineSegments.Count,
                    IsSuccess = false,
                    ErrorMessage = "线速必须大于0 - Speed must be greater than 0"
                };
            }

            totalDistanceMm += segment.LengthMm;
            totalTimeMs += segment.CalculateTransitTimeMs(segmentSpeed);
        }

        // 如果指定了全局速度，还需要计算落格偏移的时间
        if (speedMmPerSec.HasValue && dropOffsetMm > 0)
        {
            totalTimeMs += (dropOffsetMm / speedMmPerSec.Value) * 1000.0;
        }

        return new RouteTimingEstimate
        {
            ChuteId = chuteId,
            TotalDistanceMm = totalDistanceMm,
            EstimatedArrivalTimeMs = totalTimeMs,
            SpeedMmPerSec = speedMmPerSec ?? (double)topology.DefaultLineSpeedMmps,
            SegmentCount = topology.LineSegments.Count,
            IsSuccess = true,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// 计算超时阈值（毫秒）
    /// </summary>
    public double? CalculateTimeoutThreshold(string chuteId, double toleranceFactor = 1.5, double? speedMmPerSec = null)
    {
        if (toleranceFactor <= 0)
        {
            throw new ArgumentException("容忍系数必须大于0", nameof(toleranceFactor));
        }

        var estimate = EstimateArrivalTime(chuteId, speedMmPerSec);
        if (!estimate.IsSuccess)
        {
            return null;
        }

        return estimate.EstimatedArrivalTimeMs * toleranceFactor;
    }
}
