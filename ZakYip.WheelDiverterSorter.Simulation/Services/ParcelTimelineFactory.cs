using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Simulation.Configuration;

namespace ZakYip.WheelDiverterSorter.Simulation.Services;

/// <summary>
/// 包裹时间轴工厂
/// </summary>
/// <remarks>
/// 负责为每个包裹生成完整的时间轴，包括各个传感器触发的时间点。
/// 支持摩擦因子模拟（影响到达时间）和掉包模拟（中途停止事件序列）。
/// </remarks>
public class ParcelTimelineFactory
{
    private readonly SimulationOptions _options;
    private readonly ILogger<ParcelTimelineFactory> _logger;
    private readonly Random? _frictionRandom;
    private readonly Random? _dropoutRandom;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ParcelTimelineFactory(
        IOptions<SimulationOptions> options,
        ILogger<ParcelTimelineFactory> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 初始化摩擦随机数生成器
        if (_options.IsEnableRandomFriction)
        {
            var seed = _options.FrictionModel.IsDeterministic && _options.FrictionModel.Seed.HasValue
                ? _options.FrictionModel.Seed.Value
                : Environment.TickCount;
            _frictionRandom = new Random(seed);
            _logger.LogDebug("摩擦模拟已启用，种子={Seed}", seed);
        }

        // 初始化掉包随机数生成器
        if (_options.IsEnableRandomDropout)
        {
            var seed = _options.DropoutModel.Seed ?? Environment.TickCount;
            _dropoutRandom = new Random(seed);
            _logger.LogDebug("掉包模拟已启用，种子={Seed}", seed);
        }
    }

    /// <summary>
    /// 为指定包裹和路径生成时间轴
    /// </summary>
    /// <param name="parcelId">包裹ID</param>
    /// <param name="path">分拣路径</param>
    /// <param name="startTime">起始时间（入口传感器触发时间）</param>
    /// <param name="previousEntryTime">前一包裹的入口时间（用于计算头距）</param>
    /// <returns>包裹时间轴</returns>
    public ParcelTimeline GenerateTimeline(long parcelId, SwitchingPath path, DateTimeOffset startTime, DateTimeOffset? previousEntryTime = null)
    {
        var timeline = new ParcelTimeline
        {
            ParcelId = parcelId,
            TargetChuteId = path.TargetChuteId,
            EntryTime = startTime,
            SensorEvents = new List<SensorEvent>()
        };

        // 计算与前一包裹的头距（时间和空间）
        if (previousEntryTime.HasValue)
        {
            var deltaTime = startTime - previousEntryTime.Value;
            timeline.HeadwayTime = deltaTime;
            
            // 基于线速估算空间间隔
            timeline.HeadwayMm = _options.LineSpeedMmps * (decimal)deltaTime.TotalSeconds;

            // 判定是否为高密度包裹
            var isDenseByTime = _options.MinSafeHeadwayTime.HasValue 
                && deltaTime < _options.MinSafeHeadwayTime.Value;
            
            var isDenseBySpace = _options.MinSafeHeadwayMm.HasValue
                && timeline.HeadwayMm < _options.MinSafeHeadwayMm.Value;

            timeline.IsDenseParcel = isDenseByTime || isDenseBySpace;

            if (timeline.IsDenseParcel)
            {
                _logger.LogDebug(
                    "包裹 {ParcelId} 被标记为高密度：时间间隔={TimeMs:F0}ms (阈值={MinTime}ms), 空间间隔={SpaceMm:F0}mm (阈值={MinSpace}mm)",
                    parcelId,
                    deltaTime.TotalMilliseconds,
                    _options.MinSafeHeadwayTime?.TotalMilliseconds,
                    timeline.HeadwayMm,
                    _options.MinSafeHeadwayMm);
            }
        }

        var currentTime = startTime;
        var isDropped = false;
        string? dropoutLocation = null;

        // 入口传感器事件
        timeline.SensorEvents.Add(new SensorEvent
        {
            SensorId = "Entry",
            TriggerTime = currentTime,
            SegmentName = "Entry"
        });

        // 为每个路径段生成传感器事件
        for (int i = 0; i < path.Segments.Count; i++)
        {
            var segment = path.Segments[i];
            var segmentName = $"D{segment.DiverterId}";
            var previousSegmentName = i == 0 ? "Entry" : $"D{path.Segments[i - 1].DiverterId}";

            // 检查是否在此段掉包
            if (_options.IsEnableRandomDropout && !isDropped)
            {
                var segmentIdentifier = $"{previousSegmentName}-{segmentName}";
                if (ShouldDropout(segmentIdentifier))
                {
                    isDropped = true;
                    dropoutLocation = segmentIdentifier;
                    _logger.LogDebug("包裹 {ParcelId} 在 {Location} 掉包", parcelId, dropoutLocation);
                    break; // 掉包后不再生成后续事件
                }
            }

            // 计算理想到达时间（基于段的长度和线速）
            var segmentLengthMm = segment.DiverterId * 1000; // 每个摆轮间隔不同，模拟不同长度
            var idealTravelTimeMs = (decimal)segmentLengthMm / _options.LineSpeedMmps * 1000m;

            // 应用摩擦因子
            var frictionFactor = GetFrictionFactor();
            var actualTravelTimeMs = idealTravelTimeMs * frictionFactor;

            currentTime = currentTime.AddMilliseconds((double)actualTravelTimeMs);

            // 添加传感器事件
            timeline.SensorEvents.Add(new SensorEvent
            {
                SensorId = segmentName,
                TriggerTime = currentTime,
                SegmentName = segmentName,
                FrictionFactor = frictionFactor
            });

            _logger.LogTrace(
                "包裹 {ParcelId}: {Segment} 理想={IdealMs:F0}ms, 摩擦因子={Friction:F2}, 实际={ActualMs:F0}ms",
                parcelId, segmentName, idealTravelTimeMs, frictionFactor, actualTravelTimeMs);
        }

        timeline.IsDropped = isDropped;
        timeline.DropoutLocation = dropoutLocation;
        timeline.ExpectedArrivalTime = currentTime;

        return timeline;
    }

    /// <summary>
    /// 获取摩擦因子
    /// </summary>
    private decimal GetFrictionFactor()
    {
        if (!_options.IsEnableRandomFriction || _frictionRandom == null)
        {
            return 1.0m; // 无摩擦，理想状态
        }

        var minFactor = _options.FrictionModel.MinFactor;
        var maxFactor = _options.FrictionModel.MaxFactor;

        // 生成随机摩擦因子
        var randomValue = (decimal)_frictionRandom.NextDouble();
        var factor = minFactor + (maxFactor - minFactor) * randomValue;

        return factor;
    }

    /// <summary>
    /// 判断是否应该在指定段掉包
    /// </summary>
    private bool ShouldDropout(string segmentIdentifier)
    {
        if (_dropoutRandom == null)
        {
            return false;
        }

        // 检查是否在允许掉包的段列表中
        if (_options.DropoutModel.AllowedSegments != null && 
            _options.DropoutModel.AllowedSegments.Count > 0)
        {
            if (!_options.DropoutModel.AllowedSegments.Contains(segmentIdentifier))
            {
                return false; // 不在允许列表中
            }
        }

        // 根据概率判断
        var probability = (double)_options.DropoutModel.DropoutProbabilityPerSegment;
        var roll = _dropoutRandom.NextDouble();

        return roll < probability;
    }
}

/// <summary>
/// 包裹时间轴
/// </summary>
public class ParcelTimeline
{
    /// <summary>
    /// 包裹ID
    /// </summary>
    public long ParcelId { get; set; }

    /// <summary>
    /// 目标格口ID
    /// </summary>
    public int TargetChuteId { get; set; }

    /// <summary>
    /// 入口时间
    /// </summary>
    public DateTimeOffset EntryTime { get; set; }

    /// <summary>
    /// 预期到达时间（考虑摩擦因子后的时间）
    /// </summary>
    public DateTimeOffset ExpectedArrivalTime { get; set; }

    /// <summary>
    /// 传感器事件列表（按时间顺序）
    /// </summary>
    public List<SensorEvent> SensorEvents { get; set; } = new();

    /// <summary>
    /// 是否掉包
    /// </summary>
    public bool IsDropped { get; set; }

    /// <summary>
    /// 掉包位置
    /// </summary>
    public string? DropoutLocation { get; set; }

    /// <summary>
    /// 是否为高密度包裹（违反最小安全头距）
    /// </summary>
    public bool IsDenseParcel { get; set; }

    /// <summary>
    /// 与前一包裹的时间间隔（头距时间）
    /// </summary>
    public TimeSpan? HeadwayTime { get; set; }

    /// <summary>
    /// 与前一包裹的空间间隔（头距距离，单位：mm）
    /// </summary>
    public decimal? HeadwayMm { get; set; }
}

/// <summary>
/// 传感器事件
/// </summary>
public class SensorEvent
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    public required string SensorId { get; set; }

    /// <summary>
    /// 触发时间
    /// </summary>
    public DateTimeOffset TriggerTime { get; set; }

    /// <summary>
    /// 段名称
    /// </summary>
    public required string SegmentName { get; set; }

    /// <summary>
    /// 摩擦因子（用于调试）
    /// </summary>
    public decimal FrictionFactor { get; set; } = 1.0m;
}
