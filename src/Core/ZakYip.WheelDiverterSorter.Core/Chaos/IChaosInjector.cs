namespace ZakYip.WheelDiverterSorter.Core.Chaos;

/// <summary>
/// 混沌注入器接口
/// Chaos injector interface
/// </summary>
/// <remarks>
/// PR-41: Provides chaos engineering capabilities for testing system resilience
/// </remarks>
public interface IChaosInjector
{
    /// <summary>
    /// 是否启用混沌测试
    /// Whether chaos testing is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 在通讯层注入混沌
    /// Inject chaos in communication layer
    /// </summary>
    /// <param name="operationName">操作名称</param>
    /// <returns>如果应该模拟故障则返回true</returns>
    Task<bool> ShouldInjectCommunicationChaosAsync(string operationName);

    /// <summary>
    /// 在驱动层注入混沌
    /// Inject chaos in driver layer
    /// </summary>
    /// <param name="driverName">驱动名称</param>
    /// <returns>如果应该模拟故障则返回true</returns>
    Task<bool> ShouldInjectDriverChaosAsync(string driverName);

    /// <summary>
    /// 在IO层注入混沌
    /// Inject chaos in IO layer
    /// </summary>
    /// <param name="sensorId">传感器ID</param>
    /// <returns>如果应该模拟故障则返回true</returns>
    Task<bool> ShouldInjectIoChaosAsync(string sensorId);

    /// <summary>
    /// 获取通讯层延迟（毫秒）
    /// Get communication layer delay in milliseconds
    /// </summary>
    /// <returns>延迟毫秒数，如果不应延迟则返回0</returns>
    Task<int> GetCommunicationDelayAsync();

    /// <summary>
    /// 获取驱动层延迟（毫秒）
    /// Get driver layer delay in milliseconds
    /// </summary>
    /// <returns>延迟毫秒数，如果不应延迟则返回0</returns>
    Task<int> GetDriverDelayAsync();

    /// <summary>
    /// 启用混沌测试
    /// Enable chaos testing
    /// </summary>
    void Enable();

    /// <summary>
    /// 禁用混沌测试
    /// Disable chaos testing
    /// </summary>
    void Disable();

    /// <summary>
    /// 配置混沌选项
    /// Configure chaos options
    /// </summary>
    /// <param name="options">混沌选项</param>
    void Configure(ChaosInjectionOptions options);
}
