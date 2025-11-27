using ZakYip.WheelDiverterSorter.Core.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Topology.Services;

/// <summary>
/// JSON配置的设备绑定服务实现
/// </summary>
/// <remarks>
/// <para>从配置（JSON/appsettings）加载设备绑定信息。</para>
/// <para>此实现提供了基于内存的设备绑定访问能力。</para>
/// </remarks>
public sealed class JsonDeviceBindingService : IDeviceBindingService
{
    private readonly Dictionary<string, DeviceBinding> _bindings;
    private readonly Dictionary<long, IWheelDiverterDevice> _wheelDevices;
    private readonly Dictionary<long, IConveyorLineSegmentDevice> _conveyorDevices;
    private readonly Dictionary<string, IDiscreteIoPort> _ioPorts;

    /// <summary>
    /// 使用指定的绑定信息初始化设备绑定服务
    /// </summary>
    /// <param name="bindings">设备绑定列表</param>
    /// <param name="wheelDevices">摆轮设备字典（按设备ID索引）</param>
    /// <param name="conveyorDevices">输送线段设备字典（按设备ID索引）</param>
    /// <param name="ioPorts">IO端口字典（按组名+端口号索引，格式："组名:端口号"）</param>
    public JsonDeviceBindingService(
        IEnumerable<DeviceBinding>? bindings = null,
        IDictionary<long, IWheelDiverterDevice>? wheelDevices = null,
        IDictionary<long, IConveyorLineSegmentDevice>? conveyorDevices = null,
        IDictionary<string, IDiscreteIoPort>? ioPorts = null)
    {
        _bindings = bindings?.ToDictionary(b => b.NodeId, b => b) ?? new Dictionary<string, DeviceBinding>();
        _wheelDevices = wheelDevices != null 
            ? new Dictionary<long, IWheelDiverterDevice>(wheelDevices) 
            : new Dictionary<long, IWheelDiverterDevice>();
        _conveyorDevices = conveyorDevices != null 
            ? new Dictionary<long, IConveyorLineSegmentDevice>(conveyorDevices) 
            : new Dictionary<long, IConveyorLineSegmentDevice>();
        _ioPorts = ioPorts != null 
            ? new Dictionary<string, IDiscreteIoPort>(ioPorts) 
            : new Dictionary<string, IDiscreteIoPort>();
    }

    /// <inheritdoc/>
    public DeviceBinding? GetBinding(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            return null;
        }
        
        return _bindings.TryGetValue(nodeId, out var binding) ? binding : null;
    }

    /// <inheritdoc/>
    public IWheelDiverterDevice? GetWheelDevice(string nodeId)
    {
        var binding = GetBinding(nodeId);
        if (binding?.WheelDeviceId == null)
        {
            return null;
        }
        
        return _wheelDevices.TryGetValue(binding.WheelDeviceId.Value, out var device) ? device : null;
    }

    /// <inheritdoc/>
    public IConveyorLineSegmentDevice? GetConveyorDevice(string nodeId)
    {
        var binding = GetBinding(nodeId);
        if (binding?.ConveyorSegmentId == null)
        {
            return null;
        }
        
        return _conveyorDevices.TryGetValue(binding.ConveyorSegmentId.Value, out var device) ? device : null;
    }

    /// <inheritdoc/>
    public IDiscreteIoPort? GetIoPort(string nodeId)
    {
        var binding = GetBinding(nodeId);
        if (binding?.IoGroupName == null || binding.IoPortNumber == null)
        {
            return null;
        }
        
        var key = GetIoPortKey(binding.IoGroupName, binding.IoPortNumber.Value);
        return _ioPorts.TryGetValue(key, out var port) ? port : null;
    }

    /// <summary>
    /// 获取IO端口的键值
    /// </summary>
    /// <param name="groupName">IO组名称</param>
    /// <param name="portNumber">端口号</param>
    /// <returns>格式化的键值</returns>
    public static string GetIoPortKey(string groupName, int portNumber)
    {
        return $"{groupName}:{portNumber}";
    }

    /// <summary>
    /// 创建默认的设备绑定服务实例（用于测试或示例）
    /// </summary>
    /// <returns>包含示例绑定的服务实例</returns>
    public static JsonDeviceBindingService CreateDefault()
    {
        var bindings = new List<DeviceBinding>
        {
            new DeviceBinding 
            { 
                NodeId = "ENTRY", 
                IoGroupName = "MainIO", 
                IoPortNumber = 0 
            },
            new DeviceBinding 
            { 
                NodeId = "D1", 
                WheelDeviceId = 1,
                IoGroupName = "MainIO",
                IoPortNumber = 1
            },
            new DeviceBinding 
            { 
                NodeId = "D2", 
                WheelDeviceId = 2,
                IoGroupName = "MainIO",
                IoPortNumber = 2
            },
            new DeviceBinding 
            { 
                NodeId = "D3", 
                WheelDeviceId = 3,
                IoGroupName = "MainIO",
                IoPortNumber = 3
            },
            new DeviceBinding 
            { 
                NodeId = "CHUTE_1", 
                IoGroupName = "ChuteIO", 
                IoPortNumber = 1 
            },
            new DeviceBinding 
            { 
                NodeId = "CHUTE_2", 
                IoGroupName = "ChuteIO", 
                IoPortNumber = 2 
            },
            new DeviceBinding 
            { 
                NodeId = "CHUTE_3", 
                IoGroupName = "ChuteIO", 
                IoPortNumber = 3 
            },
            new DeviceBinding 
            { 
                NodeId = "CHUTE_EXCEPTION", 
                IoGroupName = "ChuteIO", 
                IoPortNumber = 999 
            }
        };

        return new JsonDeviceBindingService(bindings);
    }
}
