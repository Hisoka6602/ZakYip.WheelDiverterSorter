using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 系统配置请求模型
/// </summary>
/// <remarks>
/// 系统配置不包含通信相关字段，通信配置请使用 /api/communication/config/persisted 端点
/// </remarks>
[SwaggerSchema(Description = "系统全局配置参数，包括异常处理和分拣模式。通信相关配置请使用 /api/communication 端点")]
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

    /// <summary>
    /// Worker 后台服务配置
    /// </summary>
    /// <remarks>
    /// 统一管理所有 BackgroundService/IHostedService 的轮询间隔和异常恢复延迟配置
    /// </remarks>
    [SwaggerSchema(Description = "后台服务轮询间隔和异常恢复延迟配置")]
    public WorkerConfigRequest? Worker { get; init; }
}

/// <summary>
/// Worker 后台服务配置请求模型
/// </summary>
[SwaggerSchema(Description = "后台服务的轮询间隔和异常恢复延迟配置")]
public record WorkerConfigRequest
{
    /// <summary>
    /// 状态检查轮询间隔（毫秒）
    /// </summary>
    /// <example>500</example>
    /// <remarks>
    /// 建议范围：100ms - 1000ms。默认 500ms 平衡响应时间和资源占用
    /// </remarks>
    [Range(100, 5000, ErrorMessage = "状态检查间隔必须在 100ms - 5000ms 范围内")]
    [SwaggerSchema(Description = "后台服务检查系统状态的轮询间隔，默认 500ms")]
    public int StateCheckIntervalMs { get; init; } = 500;

    /// <summary>
    /// 异常恢复延迟（毫秒）
    /// </summary>
    /// <example>2000</example>
    /// <remarks>
    /// 建议范围：1000ms - 5000ms。默认 2000ms 避免故障时快速循环消耗资源
    /// </remarks>
    [Range(1000, 30000, ErrorMessage = "异常恢复延迟必须在 1000ms - 30000ms 范围内")]
    [SwaggerSchema(Description = "后台服务发生异常后的重试延迟，默认 2000ms")]
    public int ErrorRecoveryDelayMs { get; init; } = 2000;
}
