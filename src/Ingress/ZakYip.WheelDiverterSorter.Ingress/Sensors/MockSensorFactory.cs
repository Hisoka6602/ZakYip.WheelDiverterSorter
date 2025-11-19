using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Ingress.Configuration;

namespace ZakYip.WheelDiverterSorter.Ingress.Sensors;

/// <summary>
/// 模拟传感器工厂
/// </summary>
/// <remarks>
/// 用于创建模拟传感器实例，用于测试和调试
/// </remarks>
public class MockSensorFactory : ISensorFactory {
    private readonly ILogger<MockSensorFactory> _logger;
    private readonly List<MockSensorConfigDto> _configs;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="configs">模拟传感器配置列表</param>
    public MockSensorFactory(
        ILogger<MockSensorFactory> logger,
        List<MockSensorConfigDto> configs) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configs = configs ?? throw new ArgumentNullException(nameof(configs));
    }

    /// <summary>
    /// 创建所有配置的传感器实例
    /// </summary>
    /// <returns>传感器实例列表</returns>
    public IEnumerable<ISensor> CreateSensors() {
        var sensors = new List<ISensor>();

        _logger.LogInformation("开始创建模拟传感器，共 {Count} 个配置", _configs.Count);

        foreach (var config in _configs.Where(s => s.IsEnabled)) {
            try {
                // 使用通用 MockSensor 类替代特定类型的传感器
                var sensor = new MockSensor(
                    config.SensorId,
                    config.Type,
                    config.MinTriggerIntervalMs,
                    config.MaxTriggerIntervalMs,
                    config.MinParcelPassTimeMs,
                    config.MaxParcelPassTimeMs);

                sensors.Add(sensor);
                _logger.LogInformation(
                    "成功创建模拟传感器 {SensorId}，类型: {Type}",
                    config.SensorId,
                    config.Type);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "创建模拟传感器 {SensorId} 失败", config.SensorId);
            }
        }

        _logger.LogInformation("模拟传感器创建完成，成功创建 {Count} 个", sensors.Count);

        return sensors;
    }
}