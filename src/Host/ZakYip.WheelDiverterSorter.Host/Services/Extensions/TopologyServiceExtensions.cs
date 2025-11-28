using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology.Legacy;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology.Legacy.Services;

namespace ZakYip.WheelDiverterSorter.Host.Services.Extensions;

/// <summary>
/// 线体拓扑服务扩展方法
/// </summary>
/// <remarks>
/// <para>注册拓扑模型和设备绑定服务。</para>
/// <para>拓扑服务负责管理线体的逻辑结构（节点和边）。</para>
/// <para>设备绑定服务负责将拓扑节点与具体硬件设备关联。</para>
/// </remarks>
#pragma warning disable CS0618 // 遗留拓扑类型正在逐步迁移中
public static class TopologyServiceExtensions
{
    /// <summary>
    /// 注册线体拓扑服务和设备绑定服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <remarks>
    /// <para>注册以下服务：</para>
    /// <list type="bullet">
    /// <item><see cref="ILineTopologyService"/> - 线体拓扑服务（单例）</item>
    /// <item><see cref="IDeviceBindingService"/> - 设备绑定服务（单例）</item>
    /// </list>
    /// <para>使用默认配置初始化服务，后续可通过配置文件或API修改。</para>
    /// </remarks>
    public static IServiceCollection AddTopologyServices(this IServiceCollection services)
    {
        // 注册线体拓扑服务为单例
        // 使用默认拓扑配置，后续可通过配置文件或API修改
        services.AddSingleton<ILineTopologyService>(serviceProvider =>
        {
            return JsonLineTopologyService.CreateDefault();
        });

        // 注册设备绑定服务为单例
        // 使用默认绑定配置，后续可通过配置文件或API修改
        services.AddSingleton<IDeviceBindingService>(serviceProvider =>
        {
            return JsonDeviceBindingService.CreateDefault();
        });

        return services;
    }
}
