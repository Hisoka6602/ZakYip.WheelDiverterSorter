namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口路由配置响应模型
/// </summary>
public class RouteConfigResponse
{
    /// <summary>
    /// 配置ID
    /// </summary>
    /// <example>1</example>
    public int Id { get; set; }

    /// <summary>
    /// 目标格口标识
    /// </summary>
    /// <example>CHUTE-01</example>
    public required string ChuteId { get; set; }

    /// <summary>
    /// 摆轮配置列表
    /// </summary>
    public required List<DiverterConfigRequest> DiverterConfigurations { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    /// <example>2025-11-12T16:30:00Z</example>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    /// <example>2025-11-12T16:30:00Z</example>
    public DateTime UpdatedAt { get; set; }
}
