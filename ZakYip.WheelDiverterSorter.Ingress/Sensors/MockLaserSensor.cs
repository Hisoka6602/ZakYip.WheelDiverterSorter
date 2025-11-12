namespace ZakYip.WheelDiverterSorter.Ingress.Sensors;

/// <summary>
/// 模拟激光传感器
/// </summary>
/// <remarks>
/// 用于测试和调试，模拟真实激光传感器的行为。
/// 在生产环境中，应替换为实际的激光传感器实现，与真实硬件通信。
/// </remarks>
public class MockLaserSensor : MockSensorBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    public MockLaserSensor(string sensorId)
        : base(sensorId, SensorType.Laser)
    {
    }
}
