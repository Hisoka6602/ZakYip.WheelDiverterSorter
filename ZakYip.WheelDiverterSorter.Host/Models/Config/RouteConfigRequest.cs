using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口路由配置请求模型
/// </summary>
public class RouteConfigRequest
{
    /// <summary>
    /// 目标格口标识
    /// </summary>
    /// <example>CHUTE-01</example>
    [Required(ErrorMessage = "格口ID不能为空")]
    public required string ChuteId { get; set; }

    /// <summary>
    /// 摆轮配置列表，按顺序执行
    /// </summary>
    /// <example>
    /// [
    ///   {
    ///     "diverterId": "DIV-001",
    ///     "targetAngle": 45,
    ///     "sequenceNumber": 1
    ///   },
    ///   {
    ///     "diverterId": "DIV-002",
    ///     "targetAngle": 30,
    ///     "sequenceNumber": 2
    ///   }
    /// ]
    /// </example>
    [Required(ErrorMessage = "摆轮配置列表不能为空")]
    [MinLength(1, ErrorMessage = "至少需要一个摆轮配置")]
    public required List<DiverterConfigRequest> DiverterConfigurations { get; set; }

    /// <summary>
    /// 是否启用此配置
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; set; } = true;
}
