using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Ingress.Sensors;

/// <summary>
/// 基于雷赛（Leadshine）控制器的真实激光传感器
/// </summary>
/// <remarks>
/// 通过读取雷赛控制器的IO输入端口来检测包裹通过
/// </remarks>
public class LeadshineLaserSensor : LeadshineSensorBase
{
    /// <summary>
    /// 传感器类型
    /// </summary>
    public override SensorType Type => SensorType.Laser;

    /// <summary>
    /// 传感器名称（用于日志）
    /// </summary>
    protected override string SensorTypeName => "激光传感器";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="inputPort">输入端口</param>
    /// <param name="inputBit">输入位索引</param>
    public LeadshineLaserSensor(
        ILogger<LeadshineLaserSensor> logger,
        string sensorId,
        IInputPort inputPort,
        int inputBit)
        : base(logger, sensorId, inputPort, inputBit)
    {
    }
}
