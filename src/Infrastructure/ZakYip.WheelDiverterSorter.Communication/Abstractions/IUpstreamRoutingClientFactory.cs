using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;

namespace ZakYip.WheelDiverterSorter.Communication.Abstractions;

/// <summary>
/// 上游路由客户端工厂接口
/// </summary>
/// <remarks>
/// 使用工厂模式创建通信客户端，支持多种协议（TCP/SignalR/MQTT/HTTP）
/// PR-U1: 合并 IRuleEngineClientFactory 到 IUpstreamRoutingClientFactory
/// </remarks>
public interface IUpstreamRoutingClientFactory
{
    /// <summary>
    /// 创建上游路由客户端实例
    /// </summary>
    /// <returns>客户端实例</returns>
    IUpstreamRoutingClient CreateClient();
}
