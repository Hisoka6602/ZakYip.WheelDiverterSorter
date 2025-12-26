using Microsoft.Extensions.Logging;
using csLTDMC;
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
/// 通过雷赛控制卡的数字量输入端口读取传感器状态。
/// 逻辑点位直接映射到物理输入位。
/// </remarks>
public sealed class LeadshineSensorInputReader : ISensorInputReader
{
    private readonly ILogger<LeadshineSensorInputReader> _logger;
    private readonly IEmcController _emcController;

    public LeadshineSensorInputReader(
        IEmcController emcController,
        ILogger<LeadshineSensorInputReader> logger)
    {
        _emcController = emcController ?? throw new ArgumentNullException(nameof(emcController));
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

            if (!_emcController.IsAvailable())
            {
                _logger.LogWarning(
                    "EMC控制器不可用，无法读取传感器状态: 点位={Point}",
                    logicalPoint);
                return Task.FromResult(false);
            }

            // 直接读取输入位
            short result = LTDMC.dmc_read_inbit(_emcController.CardNo, (ushort)logicalPoint);

            if (result < 0)
            {
                _logger.LogWarning(
                    "读取传感器状态失败: 点位={Point}, 错误码={ErrorCode}",
                    logicalPoint,
                    result);
                return Task.FromResult(false);
            }

            // 高电平（1）表示有信号/触发
            bool isTriggered = result == 1;
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
    /// 优化实现：计算需要读取的位范围，使用 IInputPort 的批量读取API一次性获取所有传感器状态，
    /// 避免逐个调用 dmc_read_inbit 导致的IO阻塞。
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

            if (!_emcController.IsAvailable())
            {
                _logger.LogWarning("EMC控制器不可用，无法批量读取传感器状态");
                foreach (var point in pointsList)
                {
                    results[point] = false;
                }
                return Task.FromResult<IDictionary<int, bool>>(results);
            }

            // 如果只有少量点位（≤3个），直接逐个读取可能更高效
            if (pointsList.Count <= 3)
            {
                foreach (var point in pointsList)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    
                    if (point < 0 || point > ushort.MaxValue)
                    {
                        results[point] = false;
                        continue;
                    }

                    short result = LTDMC.dmc_read_inbit(_emcController.CardNo, (ushort)point);
                    results[point] = result > 0;
                }
                return Task.FromResult<IDictionary<int, bool>>(results);
            }

            // 对于多个点位，使用批量读取优化
            // 找出最小和最大点位，计算需要读取的范围
            int minPoint = pointsList.Min();
            int maxPoint = pointsList.Max();
            
            if (minPoint < 0 || maxPoint > ushort.MaxValue)
            {
                _logger.LogWarning(
                    "传感器逻辑点位超出有效范围: 最小={MinPoint}, 最大={MaxPoint}, 有效范围=0-{MaxValue}",
                    minPoint,
                    maxPoint,
                    ushort.MaxValue);
                
                // 对于超出范围的，回退到逐个读取并验证
                foreach (var point in pointsList)
                {
                    if (point < 0 || point > ushort.MaxValue)
                    {
                        results[point] = false;
                    }
                    else
                    {
                        short result = LTDMC.dmc_read_inbit(_emcController.CardNo, (ushort)point);
                        results[point] = result > 0;
                    }
                }
                return Task.FromResult<IDictionary<int, bool>>(results);
            }

            // 计算需要读取的端口范围（每个端口32位）
            const int BitsPerPort = 32;
            ushort startPort = (ushort)(minPoint / BitsPerPort);
            ushort endPort = (ushort)(maxPoint / BitsPerPort);
            ushort portCount = (ushort)(endPort - startPort + 1);

            // 使用批量读取API一次性读取所有涉及的端口
            uint[] portValues = new uint[portCount];
            short batchResult = LTDMC.dmc_read_inport_array(_emcController.CardNo, portCount, portValues);

            if (batchResult < 0)
            {
                _logger.LogWarning(
                    "批量读取传感器状态失败，错误码={ErrorCode}，回退到逐个读取",
                    batchResult);
                
                // 批量读取失败，回退到逐个读取
                foreach (var point in pointsList)
                {
                    short result = LTDMC.dmc_read_inbit(_emcController.CardNo, (ushort)point);
                    results[point] = result > 0;
                }
                return Task.FromResult<IDictionary<int, bool>>(results);
            }

            // 从批量读取的结果中提取各个点位的值
            foreach (var point in pointsList)
            {
                int portOffset = (point / BitsPerPort) - startPort;
                int bitInPort = point % BitsPerPort;
                
                bool isTriggered = ((portValues[portOffset] >> bitInPort) & 1) != 0;
                results[point] = isTriggered;
            }

            _logger.LogDebug(
                "批量读取了 {Count} 个传感器状态，涉及端口范围: {StartPort}-{EndPort}",
                pointsList.Count,
                startPort,
                endPort);
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
        // 对于雷赛控制卡，只要EMC控制器可用，传感器就被认为在线
        // 实际的传感器故障需要通过其他机制检测（如心跳或专用故障输入）
        return Task.FromResult(_emcController.IsAvailable());
    }
}
