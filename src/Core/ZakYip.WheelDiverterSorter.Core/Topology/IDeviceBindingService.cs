using ZakYip.WheelDiverterSorter.Core.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Topology;

/// <summary>
/// 设备绑定服务接口 - 提供拓扑节点与硬件设备的映射能力
/// </summary>
/// <remarks>
/// <para>此接口负责将拓扑节点与具体硬件设备关联。</para>
/// <para>通过此服务，可以根据拓扑节点ID获取对应的硬件设备实例。</para>
/// </remarks>
public interface IDeviceBindingService
{
    /// <summary>
    /// 获取指定节点的设备绑定信息
    /// </summary>
    /// <param name="nodeId">节点ID</param>
    /// <returns>设备绑定信息，如果节点没有绑定则返回 <see langword="null"/></returns>
    DeviceBinding? GetBinding(string nodeId);

    /// <summary>
    /// 获取指定节点绑定的摆轮设备
    /// </summary>
    /// <param name="nodeId">节点ID</param>
    /// <returns>摆轮设备实例，如果节点没有绑定摆轮设备则返回 <see langword="null"/></returns>
    IWheelDiverterDevice? GetWheelDevice(string nodeId);

    /// <summary>
    /// 获取指定节点绑定的输送线段设备
    /// </summary>
    /// <param name="nodeId">节点ID</param>
    /// <returns>输送线段设备实例，如果节点没有绑定输送线段设备则返回 <see langword="null"/></returns>
    IConveyorLineSegmentDevice? GetConveyorDevice(string nodeId);

    /// <summary>
    /// 获取指定节点绑定的离散IO端口
    /// </summary>
    /// <param name="nodeId">节点ID</param>
    /// <returns>离散IO端口实例，如果节点没有绑定IO端口则返回 <see langword="null"/></returns>
    IDiscreteIoPort? GetIoPort(string nodeId);
}
