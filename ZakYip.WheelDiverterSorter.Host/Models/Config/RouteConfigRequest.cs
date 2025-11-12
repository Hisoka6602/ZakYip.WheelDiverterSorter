namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口路由配置请求模型
/// </summary>
public class RouteConfigRequest
{
    /// <summary>
    /// 目标格口标识
    /// </summary>
    public required string ChuteId { get; set; }

    /// <summary>
    /// 摆轮配置列表，按顺序执行
    /// </summary>
    public required List<DiverterConfigRequest> DiverterConfigurations { get; set; }

    /// <summary>
    /// 是否启用此配置
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
