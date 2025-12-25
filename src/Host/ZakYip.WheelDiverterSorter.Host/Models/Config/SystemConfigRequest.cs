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
    /// 设备初次开机后延迟连接驱动的时间（秒）
    /// </summary>
    /// <example>15</example>
    [Range(0, 300, ErrorMessage = "驱动启动延迟时间必须在0-300秒之间")]
    [SwaggerSchema(Description = "系统开机后延迟N秒再连接驱动，给硬件设备足够的初始化时间。0表示立即连接")]
    public int DriverStartupDelaySeconds { get; init; } = 0;

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
    /// 启用提前触发检测功能
    /// </summary>
    /// <example>false</example>
    [SwaggerSchema(Description = "启用后，系统会在摆轮前传感器触发时检查 EarliestDequeueTime，防止包裹提前到达导致的错位问题。默认为 false（禁用）")]
    public bool EnableEarlyTriggerDetection { get; init; } = false;
}
