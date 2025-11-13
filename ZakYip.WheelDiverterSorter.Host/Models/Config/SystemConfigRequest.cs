using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 系统配置请求模型
/// </summary>
public class SystemConfigRequest
{
    /// <summary>
    /// 异常格口ID
    /// </summary>
    /// <example>999</example>
    [Required(ErrorMessage = "异常格口ID不能为空")]
    [Range(1, int.MaxValue, ErrorMessage = "异常格口ID必须大于0")]
    public int ExceptionChuteId { get; set; } = 999;

    /// <summary>
    /// MQTT默认端口
    /// </summary>
    /// <example>1883</example>
    [Range(1, 65535, ErrorMessage = "MQTT默认端口必须在1-65535之间")]
    public int MqttDefaultPort { get; set; } = 1883;

    /// <summary>
    /// TCP默认端口
    /// </summary>
    /// <example>8888</example>
    [Range(1, 65535, ErrorMessage = "TCP默认端口必须在1-65535之间")]
    public int TcpDefaultPort { get; set; } = 8888;

    /// <summary>
    /// 格口分配超时时间（毫秒）
    /// </summary>
    /// <example>10000</example>
    [Range(1000, 60000, ErrorMessage = "格口分配超时时间必须在1000-60000毫秒之间")]
    public int ChuteAssignmentTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    /// <example>5000</example>
    [Range(1000, 60000, ErrorMessage = "请求超时时间必须在1000-60000毫秒之间")]
    public int RequestTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 重试次数
    /// </summary>
    /// <example>3</example>
    [Range(0, 10, ErrorMessage = "重试次数必须在0-10之间")]
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    /// <example>1000</example>
    [Range(100, 10000, ErrorMessage = "重试延迟必须在100-10000毫秒之间")]
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    /// <example>true</example>
    public bool EnableAutoReconnect { get; set; } = true;
}
