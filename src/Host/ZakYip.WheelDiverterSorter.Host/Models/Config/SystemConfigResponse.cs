using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Enums;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 系统配置响应模型
/// System configuration response model
/// </summary>
/// <remarks>
/// 系统配置不包含通信相关字段，通信配置请使用 /api/communication/config/persisted 端点
/// </remarks>
[SwaggerSchema(Description = "系统全局配置响应，包含异常处理、分拣模式等配置参数")]
public record SystemConfigResponse
{
    /// <summary>
    /// 配置ID
    /// Configuration ID
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "配置的唯一标识符")]
    public required int Id { get; init; }

    /// <summary>
    /// 异常格口ID
    /// Exception chute ID
    /// </summary>
    /// <example>999</example>
    [SwaggerSchema(Description = "无法正常分拣时包裹落入的异常格口编号")]
    public required long ExceptionChuteId { get; init; }

    /// <summary>
    /// 分拣模式
    /// Sorting mode
    /// </summary>
    /// <example>Formal</example>
    [SwaggerSchema(Description = "系统运行模式：Formal（正式）、FixedChute（固定格口）、RoundRobin（循环格口）")]
    public required SortingMode SortingMode { get; init; }

    /// <summary>
    /// 固定格口ID（仅在指定落格分拣模式下使用）
    /// Fixed chute ID (only used in FixedChute mode)
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "固定格口模式下的目标格口编号")]
    public long? FixedChuteId { get; init; }

    /// <summary>
    /// 可用格口ID列表（仅在循环格口落格模式下使用）
    /// Available chute IDs (only used in RoundRobin mode)
    /// </summary>
    /// <example>[1, 2, 3, 4, 5]</example>
    [SwaggerSchema(Description = "循环格口模式下的可用格口编号列表")]
    public List<long> AvailableChuteIds { get; init; } = new();

    /// <summary>
    /// 配置版本号
    /// Configuration version number
    /// </summary>
    /// <example>1</example>
    [SwaggerSchema(Description = "配置的版本号，每次更新递增")]
    public required int Version { get; init; }

    /// <summary>
    /// 创建时间（本地时间）
    /// Creation time (local time)
    /// </summary>
    [SwaggerSchema(Description = "配置创建的时间")]
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// 更新时间（本地时间）
    /// Update time (local time)
    /// </summary>
    [SwaggerSchema(Description = "配置最后更新的时间")]
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Worker 后台服务配置
    /// Worker background service configuration
    /// </summary>
    /// <remarks>
    /// 统一管理所有 BackgroundService/IHostedService 的轮询间隔和异常恢复延迟配置
    /// </remarks>
    [SwaggerSchema(Description = "后台服务轮询间隔和异常恢复延迟配置")]
    public WorkerConfigResponse Worker { get; init; } = new() { StateCheckIntervalMs = 500, ErrorRecoveryDelayMs = 2000 };
}

/// <summary>
/// Worker 后台服务配置响应模型
/// Worker background service configuration response model
/// </summary>
[SwaggerSchema(Description = "后台服务的轮询间隔和异常恢复延迟配置")]
public record WorkerConfigResponse
{
    /// <summary>
    /// 状态检查轮询间隔（毫秒）
    /// State check polling interval (milliseconds)
    /// </summary>
    /// <example>500</example>
    [SwaggerSchema(Description = "后台服务检查系统状态的轮询间隔")]
    public required int StateCheckIntervalMs { get; init; }

    /// <summary>
    /// 异常恢复延迟（毫秒）
    /// Error recovery delay (milliseconds)
    /// </summary>
    /// <example>2000</example>
    [SwaggerSchema(Description = "后台服务发生异常后的重试延迟")]
    public required int ErrorRecoveryDelayMs { get; init; }
}
