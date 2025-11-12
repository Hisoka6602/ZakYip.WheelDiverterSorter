namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 表示格口路由配置，包含从入口到目标格口的摆轮配置列表
/// </summary>
public class ChuteRouteConfiguration
{
    /// <summary>
    /// LiteDB自动生成的唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 目标格口标识
    /// </summary>
    public required string ChuteId { get; set; }

    /// <summary>
    /// 摆轮配置列表，按顺序执行
    /// </summary>
    public required List<DiverterConfigurationEntry> DiverterConfigurations { get; set; }

    /// <summary>
    /// 配置创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否启用此配置
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
