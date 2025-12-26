using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine;

/// <summary>
/// 雷赛传感器输入读取器实现
/// Leadshine Sensor Input Reader Implementation
/// </summary>
/// <remarks>
/// 从IO状态缓存服务读取传感器状态。
/// 所有硬件IO读取由 LeadshineIoStateCache 后台服务集中处理。
/// </remarks>
public sealed class LeadshineSensorInputReader : ISensorInputReader
{
    private readonly ILogger<LeadshineSensorInputReader> _logger;
    private readonly ILeadshineIoStateCache _ioStateCache;

    public LeadshineSensorInputReader(
        ILeadshineIoStateCache ioStateCache,
        ILogger<LeadshineSensorInputReader> logger)
    {
        _ioStateCache = ioStateCache ?? throw new ArgumentNullException(nameof(ioStateCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<bool> ReadSensorAsync(int logicalPoint, CancellationToken cancellationToken = default)
    {
        try
        {
            // 验证逻辑点位范围
            if (logicalPoint < 0 || logicalPoint > ushort.MaxValue)
            {
                _logger.LogWarning(
                    "逻辑点位超出有效范围: 点位={Point}, 有效范围=0-{MaxValue}",
                    logicalPoint,
                    ushort.MaxValue);
                return Task.FromResult(false);
            }

            if (!_ioStateCache.IsAvailable)
            {
                _logger.LogWarning(
                    "IO状态缓存不可用，无法读取传感器状态: 点位={Point}",
                    logicalPoint);
                return Task.FromResult(false);
            }

            // 从缓存读取（非阻塞）
            bool isTriggered = _ioStateCache.ReadInputBit(logicalPoint);
            return Task.FromResult(isTriggered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取传感器状态时发生异常: 点位={Point}", logicalPoint);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 从IO状态缓存批量读取，非阻塞操作。
    /// </remarks>
    public Task<IDictionary<int, bool>> ReadSensorsAsync(
        IEnumerable<int> logicalPoints,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<int, bool>();
        
        try
        {
            var pointsList = logicalPoints.ToList();
            
            if (pointsList.Count == 0)
            {
                return Task.FromResult<IDictionary<int, bool>>(results);
            }

            if (!_ioStateCache.IsAvailable)
            {
                _logger.LogWarning("IO状态缓存不可用，无法批量读取传感器状态");
                foreach (var point in pointsList)
                {
                    results[point] = false;
                }
                return Task.FromResult<IDictionary<int, bool>>(results);
            }

            // 从缓存批量读取（非阻塞）
            results = (Dictionary<int, bool>)_ioStateCache.ReadInputBits(pointsList);
            
            _logger.LogDebug(
                "批量读取了 {Count} 个传感器状态（从缓存）",
                pointsList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量读取传感器状态时发生异常");
            
            // 异常情况下返回所有点位为false
            foreach (var point in logicalPoints)
            {
                results[point] = false;
            }
        }

        return Task.FromResult<IDictionary<int, bool>>(results);
    }

    /// <inheritdoc/>
    public Task<bool> IsSensorOnlineAsync(int logicalPoint)
    {
        // 传感器在线状态取决于IO缓存服务是否可用
        return Task.FromResult(_ioStateCache.IsAvailable);
    }
}
