namespace ZakYip.WheelDiverterSorter.Ingress;

/// <summary>
/// 传感器工厂接口
/// </summary>
/// <remarks>
/// 使用工厂模式创建传感器实例，支持多种厂商的传感器
/// </remarks>
public interface ISensorFactory
{
    /// <summary>
    /// 创建所有配置的传感器实例
    /// </summary>
    /// <returns>传感器实例列表</returns>
    IEnumerable<ISensor> CreateSensors();
}
