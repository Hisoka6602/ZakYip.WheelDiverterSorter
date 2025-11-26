using Microsoft.Extensions.Logging;
using csLTDMC;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

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
    public async Task<IDictionary<int, bool>> ReadSensorsAsync(
        IEnumerable<int> logicalPoints,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<int, bool>();

        foreach (var point in logicalPoints)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            results[point] = await ReadSensorAsync(point, cancellationToken);
        }

        return results;
    }

    /// <inheritdoc/>
    public Task<bool> IsSensorOnlineAsync(int logicalPoint)
    {
        // 对于雷赛控制卡，只要EMC控制器可用，传感器就被认为在线
        // 实际的传感器故障需要通过其他机制检测（如心跳或专用故障输入）
        return Task.FromResult(_emcController.IsAvailable());
    }
}
