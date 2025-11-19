namespace ZakYip.WheelDiverterSorter.Core.IoBinding;

/// <summary>
/// IO绑定配置文件 - 描述整条线体的IO逻辑表
/// </summary>
/// <remarks>
/// 这是厂商无关的IO绑定定义。
/// 定义了所有逻辑IO点，但不涉及实际硬件地址。
/// 厂商驱动通过VendorIoMapper将这些逻辑点映射到实际地址。
/// </remarks>
public record class IoBindingProfile
{
    /// <summary>
    /// 配置文件唯一标识符
    /// </summary>
    public required string ProfileId { get; init; }

    /// <summary>
    /// 配置文件名称
    /// </summary>
    public required string ProfileName { get; init; }

    /// <summary>
    /// 关联的拓扑ID
    /// </summary>
    /// <remarks>
    /// 必须与某个LineTopology关联
    /// </remarks>
    public required string TopologyId { get; init; }

    /// <summary>
    /// 描述说明
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 传感器绑定列表
    /// </summary>
    public IReadOnlyList<SensorBinding> SensorBindings { get; init; } = Array.Empty<SensorBinding>();

    /// <summary>
    /// 执行器绑定列表
    /// </summary>
    public IReadOnlyList<ActuatorBinding> ActuatorBindings { get; init; } = Array.Empty<ActuatorBinding>();

    /// <summary>
    /// 其他IO点列表（如指示灯、按钮等）
    /// </summary>
    public IReadOnlyList<IoPointDescriptor> OtherIoPoints { get; init; } = Array.Empty<IoPointDescriptor>();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 根据逻辑名称查找传感器绑定
    /// </summary>
    public SensorBinding? FindSensorBinding(string logicalName)
    {
        return SensorBindings.FirstOrDefault(s => s.IoPoint.LogicalName == logicalName);
    }

    /// <summary>
    /// 根据逻辑名称查找执行器绑定
    /// </summary>
    public ActuatorBinding? FindActuatorBinding(string logicalName)
    {
        return ActuatorBindings.FirstOrDefault(a => a.IoPoint.LogicalName == logicalName);
    }

    /// <summary>
    /// 获取所有IO点描述符
    /// </summary>
    public IEnumerable<IoPointDescriptor> GetAllIoPoints()
    {
        return SensorBindings.Select(s => s.IoPoint)
            .Concat(ActuatorBindings.Select(a => a.IoPoint))
            .Concat(OtherIoPoints);
    }
}
