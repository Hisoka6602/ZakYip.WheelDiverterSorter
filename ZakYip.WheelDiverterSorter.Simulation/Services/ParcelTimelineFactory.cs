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
    private readonly Random? _jitterRandom;
    private readonly DateTimeOffset _simulationStartTime;
    private int _parcelCounter;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ParcelTimelineFactory(
        IOptions<SimulationOptions> options,
        ILogger<ParcelTimelineFactory> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _simulationStartTime = DateTimeOffset.UtcNow;
        _parcelCounter = 0;

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

        // 初始化抖动随机数生成器
        if (_options.SensorFault.IsEnableSensorJitter && _options.SensorFault.JitterProbability > 0)
        {
            var seed = Environment.TickCount + 1000; // 不同种子避免与其他随机数重复
            _jitterRandom = new Random(seed);
            _logger.LogDebug("传感器抖动模拟已启用，概率={Probability}", _options.SensorFault.JitterProbability);
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
        _parcelCounter++;
        
        var timeline = new ParcelTimeline
        {
            ParcelId = parcelId,
            TargetChuteId = path.TargetChuteId,
            EntryTime = startTime,
            SensorEvents = new List<SensorEvent>(),
            IsSensorFault = false,
            HasJitter = false
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

        // 检查是否启用传感器抖动
        var shouldJitter = ShouldApplyJitter();
        if (shouldJitter)
        {
            timeline.HasJitter = true;
            _logger.LogDebug("包裹 {ParcelId} 将产生传感器抖动", parcelId);
        }

        // 入口传感器事件
        AddSensorEvent(timeline, "Entry", currentTime, "Entry", shouldJitter);

        // 检查是否是摆轮前传感器故障场景
        var isPreDiverterFault = IsSensorFaultActive();
        if (isPreDiverterFault && path.Segments.Count > 0)
        {
            timeline.IsSensorFault = true;
            _logger.LogDebug("包裹 {ParcelId} 遇到摆轮前传感器故障", parcelId);
            // 传感器故障：摆轮前传感器不触发，只有入口事件
            // 不生成后续传感器事件
        }
        else
        {
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
                AddSensorEvent(timeline, segmentName, currentTime, segmentName, shouldJitter, frictionFactor);

                _logger.LogTrace(
                    "包裹 {ParcelId}: {Segment} 理想={IdealMs:F0}ms, 摩擦因子={Friction:F2}, 实际={ActualMs:F0}ms",
                    parcelId, segmentName, idealTravelTimeMs, frictionFactor, actualTravelTimeMs);
            }
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

    /// <summary>
    /// 判断是否应用传感器抖动
    /// </summary>
    private bool ShouldApplyJitter()
    {
        if (!_options.SensorFault.IsEnableSensorJitter)
        {
            return false;
        }

        // 如果概率为1.0，所有包裹都抖动
        if (_options.SensorFault.JitterProbability >= 1.0m)
        {
            return true;
        }

        // 如果概率为0，不抖动
        if (_options.SensorFault.JitterProbability <= 0)
        {
            return false;
        }

        // 根据概率判断
        if (_jitterRandom != null)
        {
            var roll = (decimal)_jitterRandom.NextDouble();
            return roll < _options.SensorFault.JitterProbability;
        }

        return false;
    }

    /// <summary>
    /// 判断传感器故障是否激活
    /// </summary>
    private bool IsSensorFaultActive()
    {
        if (!_options.SensorFault.IsPreDiverterSensorFault)
        {
            return false;
        }

        var elapsed = DateTimeOffset.UtcNow - _simulationStartTime;

        // 检查是否在故障时间范围内
        if (_options.SensorFault.FaultStartOffset.HasValue)
        {
            if (elapsed < _options.SensorFault.FaultStartOffset.Value)
            {
                return false; // 还未开始故障
            }
        }

        if (_options.SensorFault.FaultDuration.HasValue && _options.SensorFault.FaultStartOffset.HasValue)
        {
            var faultEndTime = _options.SensorFault.FaultStartOffset.Value + _options.SensorFault.FaultDuration.Value;
            if (elapsed > faultEndTime)
            {
                return false; // 故障已结束
            }
        }

        return true;
    }

    /// <summary>
    /// 添加传感器事件（支持抖动）
    /// </summary>
    private void AddSensorEvent(ParcelTimeline timeline, string sensorId, DateTimeOffset triggerTime, 
        string segmentName, bool shouldJitter, decimal frictionFactor = 1.0m)
    {
        // 添加主传感器事件
        timeline.SensorEvents.Add(new SensorEvent
        {
            SensorId = sensorId,
            TriggerTime = triggerTime,
            SegmentName = segmentName,
            FrictionFactor = frictionFactor
        });

        // 如果启用抖动，添加额外的触发事件
        if (shouldJitter)
        {
            var jitterCount = _options.SensorFault.JitterTriggerCount - 1; // 减去主事件
            var jitterIntervalMs = _options.SensorFault.JitterIntervalMs;

            for (int j = 1; j <= jitterCount; j++)
            {
                var jitterTime = triggerTime.AddMilliseconds(j * jitterIntervalMs / (double)jitterCount);
                timeline.SensorEvents.Add(new SensorEvent
                {
                    SensorId = sensorId + $"_Jitter{j}",
                    TriggerTime = jitterTime,
                    SegmentName = segmentName,
                    FrictionFactor = frictionFactor
                });
            }
        }
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

    /// <summary>
    /// 是否存在传感器故障
    /// </summary>
    public bool IsSensorFault { get; set; }

    /// <summary>
    /// 是否产生传感器抖动
    /// </summary>
    public bool HasJitter { get; set; }
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
