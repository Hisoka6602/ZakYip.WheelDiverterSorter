using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.IoBinding;

/// <summary>
/// 传感器绑定 - 描述传感器的逻辑绑定
/// </summary>
public record class SensorBinding
{
    /// <summary>
    /// IO点描述符
    /// </summary>
    public required IoPointDescriptor IoPoint { get; init; }

    /// <summary>
    /// 传感器类型
    /// </summary>
    public required SensorBindingType SensorType { get; init; }

    /// <summary>
    /// 关联的节点ID（仅当SensorType为Node时）
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// 关联的格口ID（仅当SensorType为Chute时）
    /// </summary>
    public string? ChuteId { get; init; }

    /// <summary>
    /// 触发延迟（毫秒）
    /// </summary>
    /// <remarks>
    /// 防抖动时间，传感器稳定后才触发事件
    /// </remarks>
    public int DebounceMs { get; init; } = 50;

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }
}
