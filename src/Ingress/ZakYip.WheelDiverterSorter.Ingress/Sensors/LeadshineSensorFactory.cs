using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Ingress.Sensors;

/// <summary>
/// 雷赛传感器工厂
/// </summary>
/// <remarks>
/// 基于雷赛控制器的IO端口创建真实传感器实例。
/// 使用厂商无关的 ISensorVendorConfigProvider 获取配置。
/// </remarks>
public class LeadshineSensorFactory : ISensorFactory {
    private readonly ILogger<LeadshineSensorFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IInputPort _inputPort;
    private readonly ISensorVendorConfigProvider _configProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="inputPort">输入端口</param>
    /// <param name="configProvider">传感器配置提供者</param>
    public LeadshineSensorFactory(
        ILogger<LeadshineSensorFactory> logger,
        ILoggerFactory loggerFactory,
        IInputPort inputPort,
        ISensorVendorConfigProvider configProvider) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _inputPort = inputPort ?? throw new ArgumentNullException(nameof(inputPort));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    /// <summary>
    /// 创建所有配置的传感器实例
    /// </summary>
    /// <returns>传感器实例列表</returns>
    public IEnumerable<ISensor> CreateSensors() {
        var sensors = new List<ISensor>();
        var sensorConfigs = _configProvider.GetSensorConfigs();

        _logger.LogInformation("开始创建雷赛传感器，共 {Count} 个配置", sensorConfigs.Count);

        foreach (var config in sensorConfigs.Where(s => s.IsEnabled)) {
            try {
                // 解析传感器类型
                if (!Enum.TryParse<SensorType>(config.SensorTypeName, out var sensorType)) {
                    var validTypes = string.Join(", ", Enum.GetNames<SensorType>());
                    _logger.LogWarning(
                        "无法解析传感器类型: {TypeName}，跳过传感器 {SensorId}。有效类型: {ValidTypes}",
                        config.SensorTypeName, config.SensorId, validTypes);
                    continue;
                }

                var sensor = new LeadshineSensor(
                    _loggerFactory.CreateLogger<LeadshineSensor>(),
                    config.SensorId,
                    sensorType,
                    _inputPort,
                    config.InputBit);

                sensors.Add(sensor);
                _logger.LogInformation(
                    "成功创建雷赛传感器 {SensorId}，类型: {Type}，输入位: {InputBit}",
                    config.SensorId,
                    sensorType,
                    config.InputBit);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "创建雷赛传感器 {SensorId} 失败", config.SensorId);
            }
        }

        _logger.LogInformation("雷赛传感器创建完成，成功创建 {Count} 个", sensors.Count);

        return sensors;
    }
}