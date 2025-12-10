using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine.Configuration;

/// <summary>
/// 基于数据库配置的雷赛传感器配置提供者
/// </summary>
/// <remarks>
/// 从 LiteDB 中的 SensorConfiguration 读取配置，
/// 将业务层的 SensorIoType 映射到硬件层的 SensorType。
/// 
/// **映射规则**：
/// - 所有 SensorIoType 统一映射到 SensorType.Photoelectric（光电传感器）
/// - 因为 SensorIoType 是业务功能分类（创建包裹、摆轮前、锁格），
///   而 SensorType 是硬件类型（光电、激光），两者是正交的概念
/// </remarks>
public sealed class DatabaseBackedLeadshineSensorVendorConfigProvider : ISensorVendorConfigProvider
{
    private readonly ISensorConfigurationRepository _sensorRepository;
    private readonly ILogger<DatabaseBackedLeadshineSensorVendorConfigProvider> _logger;
    private readonly ushort _cardNo;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sensorRepository">传感器配置仓储</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="cardNo">雷赛控制卡卡号</param>
    public DatabaseBackedLeadshineSensorVendorConfigProvider(
        ISensorConfigurationRepository sensorRepository,
        ILogger<DatabaseBackedLeadshineSensorVendorConfigProvider> logger,
        ushort cardNo = 0)
    {
        _sensorRepository = sensorRepository ?? throw new ArgumentNullException(nameof(sensorRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cardNo = cardNo;
    }

    /// <inheritdoc/>
    public string VendorTypeName => "Leadshine";

    /// <inheritdoc/>
    public ushort CardNo => _cardNo;

    /// <inheritdoc/>
    public IReadOnlyList<SensorConfigEntry> GetSensorConfigs()
    {
        try
        {
            var sensorConfig = _sensorRepository.Get();
            
            if (sensorConfig?.Sensors == null || !sensorConfig.Sensors.Any())
            {
                _logger.LogWarning("传感器配置为空或未找到，返回空列表");
                return Array.Empty<SensorConfigEntry>();
            }

            var result = sensorConfig.Sensors
                .Where(s => s.IsEnabled)
                .Select(s => new SensorConfigEntry
                {
                    SensorId = s.SensorId.ToString(),
                    // 将业务类型 SensorIoType 映射到硬件类型 SensorType
                    // 目前统一使用 Photoelectric（光电传感器）作为默认硬件类型
                    SensorTypeName = MapIoTypeToSensorType(s.IoType).ToString(),
                    InputBit = s.BitNumber,
                    IsEnabled = s.IsEnabled,
                    PollingIntervalMs = s.PollingIntervalMs
                })
                .ToList()
                .AsReadOnly();

            _logger.LogInformation(
                "从数据库加载传感器配置成功，共 {Count} 个启用的传感器",
                result.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从数据库加载传感器配置失败");
            return Array.Empty<SensorConfigEntry>();
        }
    }

    /// <summary>
    /// 将业务类型 SensorIoType 映射到硬件类型 SensorType
    /// </summary>
    /// <param name="ioType">业务IO类型</param>
    /// <returns>硬件传感器类型</returns>
    /// <remarks>
    /// SensorIoType 是业务功能分类（创建包裹、摆轮前、锁格），
    /// SensorType 是硬件类型（光电、激光）。
    /// 目前统一映射到 Photoelectric（光电传感器）。
    /// </remarks>
    private static SensorType MapIoTypeToSensorType(SensorIoType ioType)
    {
        // 目前所有业务类型都使用光电传感器
        // 未来如果需要支持不同硬件类型，可以在 SensorConfiguration 中添加硬件类型字段
        return SensorType.Photoelectric;
    }
}
