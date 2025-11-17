using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Ingress.Sensors;

/// <summary>
/// 模拟激光传感器
/// </summary>
/// <remarks>
/// 用于测试和调试，模拟真实激光传感器的行为。
/// 在生产环境中，应替换为实际的激光传感器实现，与真实硬件通信。
/// 此类现在是 MockSensor 的别名，保持向后兼容。
/// </remarks>
[Obsolete("Use MockSensor with SensorType.Laser instead. This class will be removed in a future version.")]
public class MockLaserSensor : MockSensor {

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <param name="minTriggerIntervalMs">模拟触发最小间隔（毫秒）</param>
    /// <param name="maxTriggerIntervalMs">模拟触发最大间隔（毫秒）</param>
    /// <param name="minParcelPassTimeMs">模拟包裹通过最小时间（毫秒）</param>
    /// <param name="maxParcelPassTimeMs">模拟包裹通过最大时间（毫秒）</param>
    public MockLaserSensor(
        string sensorId,
        int minTriggerIntervalMs = 5000,
        int maxTriggerIntervalMs = 15000,
        int minParcelPassTimeMs = 200,
        int maxParcelPassTimeMs = 500)
        : base(sensorId, SensorType.Laser, minTriggerIntervalMs, maxTriggerIntervalMs, minParcelPassTimeMs, maxParcelPassTimeMs) {
    }
}