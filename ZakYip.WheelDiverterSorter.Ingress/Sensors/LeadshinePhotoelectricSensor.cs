using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Ingress.Sensors;

/// <summary>
/// 基于雷赛（Leadshine）控制器的真实光电传感器
/// </summary>
/// <remarks>
/// 通过读取雷赛控制器的IO输入端口来检测包裹通过
/// 此类现在是 LeadshineSensor 的别名，保持向后兼容。
/// </remarks>
[Obsolete("Use LeadshineSensor with SensorType.Photoelectric instead. This class will be removed in a future version.")]
public class LeadshinePhotoelectricSensor : LeadshineSensor {

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="inputPort">输入端口</param>
    /// <param name="inputBit">输入位索引</param>
    public LeadshinePhotoelectricSensor(
        ILogger<LeadshinePhotoelectricSensor> logger,
        string sensorId,
        IInputPort inputPort,
        int inputBit)
        : base(logger, sensorId, SensorType.Photoelectric, inputPort, inputBit) {
    }
}