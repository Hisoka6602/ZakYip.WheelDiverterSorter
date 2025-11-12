namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

/// <summary>
/// RuleEngine客户端工厂接口
/// </summary>
/// <remarks>
/// 使用工厂模式创建通信客户端，支持多种协议
/// 实现低耦合，便于扩展新的通信协议
/// </remarks>
public interface IRuleEngineClientFactory
{
    /// <summary>
    /// 创建RuleEngine客户端实例
    /// </summary>
    /// <returns>客户端实例</returns>
    IRuleEngineClient CreateClient();
}
