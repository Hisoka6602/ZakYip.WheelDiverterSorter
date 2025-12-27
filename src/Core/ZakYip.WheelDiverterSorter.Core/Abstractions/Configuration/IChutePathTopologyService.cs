using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

/// <summary>
/// 格口路径拓扑服务接口（Core层 - 只读）
/// </summary>
/// <remarks>
/// <para><b>架构定位</b>：</para>
/// <list type="bullet">
///   <item>定义在 Core 层，供所有业务层（Execution、Application）使用</item>
///   <item>实现在 Application 层（通过 ISlidingConfigCache 提供缓存支持）</item>
///   <item>确保所有拓扑配置读取从内存缓存获取，避免高频 LiteDB 访问</item>
/// </list>
///
/// <para><b>缓存策略</b>：</para>
/// <list type="bullet">
///   <item>滑动过期时间：1 小时（无绝对过期）</item>
///   <item>热更新支持：配置更新后立即刷新缓存</item>
///   <item>单例模式：确保全局配置一致性</item>
/// </list>
/// </remarks>
public interface IChutePathTopologyService
{
    /// <summary>
    /// 获取格口路径拓扑配置（从缓存）
    /// </summary>
    /// <returns>当前的格口路径拓扑配置</returns>
    ChutePathTopologyConfig GetTopology();
}
