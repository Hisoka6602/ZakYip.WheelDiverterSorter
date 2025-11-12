namespace ZakYip.WheelDiverterSorter.Ingress.Sensors;

/// <summary>
/// 模拟光电传感器
/// </summary>
/// <remarks>
/// 用于测试和调试，模拟真实光电传感器的行为。
/// 在生产环境中，应替换为实际的光电传感器实现，与真实硬件通信。
/// </remarks>
public class MockPhotoelectricSensor : MockSensorBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    public MockPhotoelectricSensor(string sensorId)
        : base(sensorId, SensorType.Photoelectric)
    {
    }
}
