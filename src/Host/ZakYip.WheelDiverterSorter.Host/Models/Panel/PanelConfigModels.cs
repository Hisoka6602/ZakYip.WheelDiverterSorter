using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.Models.Panel;

/// <summary>
/// 面板配置请求模型
/// </summary>
/// <remarks>
/// 用于更新面板配置的请求数据传输对象，参数按功能分类组织
/// </remarks>
public sealed record PanelConfigRequest
{
    /// <summary>
    /// 是否启用面板功能
    /// </summary>
    /// <example>true</example>
    [Required]
    public required bool Enabled { get; init; }

    /// <summary>
    /// 是否使用仿真模式
    /// </summary>
    /// <remarks>
    /// true: 使用仿真驱动（SimulatedPanelInputReader / SimulatedSignalTowerOutput）
    /// false: 使用真实硬件驱动
    /// </remarks>
    /// <example>true</example>
    [Required]
    public required bool UseSimulation { get; init; }

    /// <summary>
    /// 面板按钮轮询间隔（毫秒）
    /// </summary>
    /// <remarks>
    /// 有效范围：50-1000 毫秒
    /// 建议值：100 毫秒
    /// </remarks>
    /// <example>100</example>
    [Required]
    [Range(50, 1000, ErrorMessage = "轮询间隔必须在 50-1000 毫秒之间")]
    public required int PollingIntervalMs { get; init; }

    /// <summary>
    /// 按钮防抖时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 有效范围：10-500 毫秒
    /// 建议值：50 毫秒
    /// 在此时间内的重复触发将被忽略
    /// </remarks>
    /// <example>50</example>
    [Required]
    [Range(10, 500, ErrorMessage = "防抖时间必须在 10-500 毫秒之间")]
    public required int DebounceMs { get; init; }

    /// <summary>
    /// 开始按钮配置
    /// </summary>
    public StartButtonConfigDto? StartButton { get; init; }

    /// <summary>
    /// 停止按钮配置
    /// </summary>
    public StopButtonConfigDto? StopButton { get; init; }

    /// <summary>
    /// 急停按钮配置
    /// </summary>
    public EmergencyStopButtonConfigDto? EmergencyStopButton { get; init; }

    /// <summary>
    /// 连接状态指示灯配置
    /// </summary>
    public ConnectionIndicatorConfigDto? ConnectionIndicator { get; init; }

    /// <summary>
    /// 三色信号塔配置
    /// </summary>
    public SignalTowerConfigDto? SignalTower { get; init; }

    /// <summary>
    /// 运行前预警配置
    /// </summary>
    public PreStartWarningConfigDto? PreStartWarning { get; init; }
}

/// <summary>
/// 面板配置响应模型
/// </summary>
/// <remarks>
/// 面板配置的数据传输对象，用于查询返回，参数按功能分类组织
/// </remarks>
public sealed record PanelConfigResponse
{
    /// <summary>
    /// 是否启用面板功能
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// 是否使用仿真模式
    /// </summary>
    public required bool UseSimulation { get; init; }

    /// <summary>
    /// 面板按钮轮询间隔（毫秒）
    /// </summary>
    public required int PollingIntervalMs { get; init; }

    /// <summary>
    /// 按钮防抖时间（毫秒）
    /// </summary>
    public required int DebounceMs { get; init; }

    /// <summary>
    /// 开始按钮配置
    /// </summary>
    public required StartButtonConfigDto StartButton { get; init; }

    /// <summary>
    /// 停止按钮配置
    /// </summary>
    public required StopButtonConfigDto StopButton { get; init; }

    /// <summary>
    /// 急停按钮配置
    /// </summary>
    public required EmergencyStopButtonConfigDto EmergencyStopButton { get; init; }

    /// <summary>
    /// 连接状态指示灯配置
    /// </summary>
    public required ConnectionIndicatorConfigDto ConnectionIndicator { get; init; }

    /// <summary>
    /// 三色信号塔配置
    /// </summary>
    public required SignalTowerConfigDto SignalTower { get; init; }

    /// <summary>
    /// 运行前预警配置
    /// </summary>
    public required PreStartWarningConfigDto PreStartWarning { get; init; }
}

/// <summary>
/// 按钮配置基类
/// </summary>
/// <remarks>
/// PR-CONFIG-HOTRELOAD02: 提取公共按钮配置，避免 StartButtonConfigDto 和 StopButtonConfigDto 结构重复
/// </remarks>
[SwaggerSchema(Description = "通用按钮的输入和指示灯输出配置")]
public record ButtonConfigDto
{
    /// <summary>
    /// 按钮输入 IO 位
    /// </summary>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? InputBit { get; init; }

    /// <summary>
    /// 按钮触发电平
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平有效（常开按键）
    /// - ActiveLow: 低电平有效（常闭按键）
    /// </remarks>
    public TriggerLevel InputTriggerLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>
    /// 指示灯输出 IO 位
    /// </summary>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? LightOutputBit { get; init; }

    /// <summary>
    /// 指示灯输出电平
    /// </summary>
    /// <remarks>
    /// - ActiveHigh: 高电平点亮（输出1）
    /// - ActiveLow: 低电平点亮（输出0）
    /// </remarks>
    public TriggerLevel LightOutputLevel { get; init; } = TriggerLevel.ActiveHigh;
}

/// <summary>
/// 开始按钮配置
/// </summary>
[SwaggerSchema(Description = "开始按钮的输入和指示灯输出配置")]
public sealed record StartButtonConfigDto : ButtonConfigDto
{
    /// <summary>
    /// 是否为开始按钮（固定为true，用于结构区分）
    /// </summary>
    /// <remarks>
    /// PR-CONFIG-HOTRELOAD02: 此属性仅用于区分 StartButtonConfigDto 和其他按钮配置的结构
    /// </remarks>
    public bool IsStartButton { get; init; } = true;
}

/// <summary>
/// 停止按钮配置
/// </summary>
[SwaggerSchema(Description = "停止按钮的输入和指示灯输出配置")]
public sealed record StopButtonConfigDto : ButtonConfigDto
{
    /// <summary>
    /// 是否为停止按钮（固定为true，用于结构区分）
    /// </summary>
    /// <remarks>
    /// PR-CONFIG-HOTRELOAD02: 此属性仅用于区分 StopButtonConfigDto 和其他按钮配置的结构
    /// </remarks>
    public bool IsStopButton { get; init; } = true;
}

/// <summary>
/// 急停按钮配置
/// </summary>
[SwaggerSchema(Description = "急停按钮的输入配置")]
public sealed record EmergencyStopButtonConfigDto
{
    /// <summary>
    /// 按钮输入 IO 位
    /// </summary>
    /// <example>2</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? InputBit { get; init; }

    /// <summary>
    /// 按钮触发电平
    /// </summary>
    public TriggerLevel InputTriggerLevel { get; init; } = TriggerLevel.ActiveHigh;
}

/// <summary>
/// 连接状态指示灯配置
/// </summary>
[SwaggerSchema(Description = "连接状态指示灯的输出配置")]
public sealed record ConnectionIndicatorConfigDto
{
    /// <summary>
    /// 指示灯输出 IO 位
    /// </summary>
    /// <example>2</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? OutputBit { get; init; }

    /// <summary>
    /// 指示灯输出电平
    /// </summary>
    public TriggerLevel OutputLevel { get; init; } = TriggerLevel.ActiveHigh;
}

/// <summary>
/// 三色信号塔配置
/// </summary>
[SwaggerSchema(Description = "三色信号塔（红、黄、绿）的输出配置")]
public sealed record SignalTowerConfigDto
{
    /// <summary>
    /// 红灯输出 IO 位
    /// </summary>
    /// <example>3</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? RedOutputBit { get; init; }

    /// <summary>
    /// 红灯输出电平
    /// </summary>
    public TriggerLevel RedOutputLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>
    /// 黄灯输出 IO 位
    /// </summary>
    /// <example>4</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? YellowOutputBit { get; init; }

    /// <summary>
    /// 黄灯输出电平
    /// </summary>
    public TriggerLevel YellowOutputLevel { get; init; } = TriggerLevel.ActiveHigh;

    /// <summary>
    /// 绿灯输出 IO 位
    /// </summary>
    /// <example>5</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? GreenOutputBit { get; init; }

    /// <summary>
    /// 绿灯输出电平
    /// </summary>
    public TriggerLevel GreenOutputLevel { get; init; } = TriggerLevel.ActiveHigh;
}

/// <summary>
/// 运行前预警配置
/// </summary>
[SwaggerSchema(Description = "启动前预警（如闪灯、蜂鸣）的配置")]
public sealed record PreStartWarningConfigDto
{
    /// <summary>
    /// 预警持续时间（秒）
    /// </summary>
    /// <remarks>
    /// 按下启动按钮后，先触发预警输出持续N秒，然后才真正启动
    /// 用于警告现场人员离开设备，避免安全事故
    /// </remarks>
    /// <example>5</example>
    [Range(0, 60, ErrorMessage = "运行前预警时间必须在 0-60 秒之间")]
    public int? DurationSeconds { get; init; }

    /// <summary>
    /// 预警输出 IO 位
    /// </summary>
    /// <example>6</example>
    [Range(0, 1023, ErrorMessage = "IO位必须在 0-1023 之间")]
    public int? OutputBit { get; init; }

    /// <summary>
    /// 预警输出电平
    /// </summary>
    public TriggerLevel OutputLevel { get; init; } = TriggerLevel.ActiveHigh;
}
