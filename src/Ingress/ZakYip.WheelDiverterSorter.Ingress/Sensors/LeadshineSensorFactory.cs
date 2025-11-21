using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Ingress.Configuration;

namespace ZakYip.WheelDiverterSorter.Ingress.Sensors;

/// <summary>
/// 雷赛传感器工厂
/// </summary>
/// <remarks>
/// 基于雷赛控制器的IO端口创建真实传感器实例
/// </remarks>
public class LeadshineSensorFactory : ISensorFactory {
    private readonly ILogger<LeadshineSensorFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IInputPort _inputPort;
    private readonly LeadshineSensorOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="inputPort">输入端口</param>
    /// <param name="options">雷赛传感器配置</param>
    public LeadshineSensorFactory(
        ILogger<LeadshineSensorFactory> logger,
        ILoggerFactory loggerFactory,
        IInputPort inputPort,
        LeadshineSensorOptions options) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _inputPort = inputPort ?? throw new ArgumentNullException(nameof(inputPort));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 创建所有配置的传感器实例
    /// </summary>
    /// <returns>传感器实例列表</returns>
    public IEnumerable<ISensor> CreateSensors() {
        var sensors = new List<ISensor>();

        _logger.LogInformation("开始创建雷赛传感器，共 {Count} 个配置", _options.Sensors.Count);

        foreach (var config in _options.Sensors.Where(s => s.IsEnabled)) {
            try {
                // 使用通用 LeadshineSensor 类替代特定类型的传感器
                var sensor = new LeadshineSensor(
                    _loggerFactory.CreateLogger<LeadshineSensor>(),
                    config.SensorId,
                    config.Type,
                    _inputPort,
                    config.InputBit);

                sensors.Add(sensor);
                _logger.LogInformation(
                    "成功创建雷赛传感器 {SensorId}，类型: {Type}，输入位: {InputBit}",
                    config.SensorId,
                    config.Type,
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