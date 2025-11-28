namespace ZakYip.WheelDiverterSorter.Core.LineModel.Topology.Legacy;

/// <summary>
/// 设备绑定 - 将拓扑节点与实际硬件设备关联
/// </summary>
/// <remarks>
/// <para>【遗留类型】此类型为遗留拓扑实现，后续版本中将移除。</para>
/// <para>设备绑定表示拓扑节点与具体硬件设备的映射关系。</para>
/// <para>通过此绑定，可以将抽象的拓扑节点与实际的IO端口、摆轮设备、输送线段等关联。</para>
/// <para>所有设备ID均使用 <see langword="long"/> 类型。</para>
/// </remarks>
[Obsolete("遗留拓扑实现，后续版本中将移除")]
public sealed record DeviceBinding
{
    /// <summary>
    /// 拓扑节点ID
    /// </summary>
    /// <remarks>
    /// 必须是拓扑中已存在的节点ID
    /// </remarks>
    /// <example>N001</example>
    public required string NodeId { get; init; }

    /// <summary>
    /// IO组名称（可选）
    /// </summary>
    /// <remarks>
    /// 用于标识IO组，如 "MainIO", "SensorIO"
    /// </remarks>
    /// <example>MainIO</example>
    public string? IoGroupName { get; init; }

    /// <summary>
    /// IO端口号（可选）
    /// </summary>
    /// <remarks>
    /// 设备级别的IO端口号
    /// </remarks>
    /// <example>10</example>
    public int? IoPortNumber { get; init; }

    /// <summary>
    /// 摆轮设备ID（可选）
    /// </summary>
    /// <remarks>
    /// 关联的摆轮设备唯一标识符，使用 <see langword="long"/> 类型
    /// </remarks>
    /// <example>1001</example>
    public long? WheelDeviceId { get; init; }

    /// <summary>
    /// 输送线段设备ID（可选）
    /// </summary>
    /// <remarks>
    /// 关联的输送线段设备唯一标识符，使用 <see langword="long"/> 类型
    /// </remarks>
    /// <example>2001</example>
    public long? ConveyorSegmentId { get; init; }
}
