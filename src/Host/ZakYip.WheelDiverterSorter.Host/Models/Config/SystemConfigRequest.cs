using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 系统配置请求模型
/// </summary>
[SwaggerSchema(Description = "系统全局配置参数，包括异常处理、通信参数和分拣模式")]
public record SystemConfigRequest
{
    /// <summary>
    /// 异常格口ID
    /// </summary>
    /// <example>999</example>
    [Required(ErrorMessage = "异常格口ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "异常格口ID必须大于0")]
    [SwaggerSchema(Description = "无法正常分拣时包裹落入的异常格口编号")]
    public long ExceptionChuteId { get; init; } = 999;

    /// <summary>
    /// MQTT默认端口
    /// </summary>
    /// <example>1883</example>
    [Range(1, 65535, ErrorMessage = "MQTT默认端口必须在1-65535之间")]
    [SwaggerSchema(Description = "MQTT协议使用的默认端口号")]
    public int MqttDefaultPort { get; init; } = 1883;

    /// <summary>
    /// TCP默认端口
    /// </summary>
    /// <example>8888</example>
    [Range(1, 65535, ErrorMessage = "TCP默认端口必须在1-65535之间")]
    [SwaggerSchema(Description = "TCP协议使用的默认端口号")]
    public int TcpDefaultPort { get; init; } = 8888;

    /// <summary>
    /// 格口分配超时时间（毫秒）
    /// </summary>
    /// <example>10000</example>
    [Range(1000, 60000, ErrorMessage = "格口分配超时时间必须在1000-60000毫秒之间")]
    [SwaggerSchema(Description = "等待上游分配格口的超时时间，单位：毫秒")]
    public int ChuteAssignmentTimeoutMs { get; init; } = 10000;

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    /// <example>5000</example>
    [Range(1000, 60000, ErrorMessage = "请求超时时间必须在1000-60000毫秒之间")]
    [SwaggerSchema(Description = "通信请求的超时时间，单位：毫秒")]
    public int RequestTimeoutMs { get; init; } = 5000;

    /// <summary>
    /// 重试次数
    /// </summary>
    /// <example>3</example>
    [Range(0, 10, ErrorMessage = "重试次数必须在0-10之间")]
    [SwaggerSchema(Description = "通信失败时的重试次数")]
    public int RetryCount { get; init; } = 3;

    /// <summary>
    /// 重试延迟（毫秒）
    /// </summary>
    /// <example>1000</example>
    [Range(100, 10000, ErrorMessage = "重试延迟必须在100-10000毫秒之间")]
    [SwaggerSchema(Description = "重试之间的延迟时间，单位：毫秒")]
    public int RetryDelayMs { get; init; } = 1000;

    /// <summary>
    /// 是否启用自动重连
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema(Description = "连接断开后是否自动重连")]
    public bool EnableAutoReconnect { get; init; } = true;

    /// <summary>
    /// 分拣模式
    /// </summary>
    /// <example>Formal</example>
    [SwaggerSchema(Description = "系统运行模式，可选值：Formal（正式模式）、FixedChute（固定格口）、RoundRobin（循环格口）")]
    public SortingMode SortingMode { get; init; } = SortingMode.Formal;

    /// <summary>
    /// 固定格口ID（仅在指定落格分拣模式下使用）
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "固定格口模式下的目标格口编号，仅在SortingMode为FixedChute时有效")]
    public long? FixedChuteId { get; init; }

    /// <summary>
    /// 可用格口ID列表（仅在循环格口落格模式下使用）
    /// </summary>
    /// <example>[1, 2, 3, 4, 5, 6]</example>
    [SwaggerSchema(Description = "循环格口模式下的可用格口编号列表，仅在SortingMode为RoundRobin时有效")]
    public List<long> AvailableChuteIds { get; init; } = new();
}
