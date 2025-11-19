namespace ZakYip.WheelDiverterSorter.Core.LineModel;

/// <summary>
/// 中段皮带段标识符。
/// 用于唯一标识一段中段皮带（例如：中段1、中段2、回流段等）。
/// </summary>
public record class ConveyorSegmentId
{
    /// <summary>
    /// 皮带段的唯一键值（例如："Middle1", "Middle2", "ReturnLoop"）
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// 皮带段的显示名称（中文，例如："中段皮带1", "中段皮带2", "回流段"）
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 段位优先级（用于联动启停顺序，数值越小优先级越高）
    /// </summary>
    public int Priority { get; init; } = 0;
}
