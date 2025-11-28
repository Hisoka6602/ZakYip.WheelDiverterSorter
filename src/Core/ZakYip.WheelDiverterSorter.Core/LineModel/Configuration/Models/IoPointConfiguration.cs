using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 统一的 IO 点配置模型
/// </summary>
/// <remarks>
/// 用于统一管理所有 IO 配置，包括面板按钮、传感器、IoLinkage 等。
/// 包含板卡标识、通道编号、IO 类型和电平语义。
/// </remarks>
public sealed record class IoPointConfiguration
{
    /// <summary>
    /// IO 点名称/标识符
    /// </summary>
    /// <example>StartButton, Sensor01, Conveyor_Enable</example>
    [Required(ErrorMessage = "IO 点名称不能为空")]
    [StringLength(100, ErrorMessage = "IO 点名称长度不能超过 100 个字符")]
    public required string Name { get; init; }

    /// <summary>
    /// 板卡/模块标识
    /// </summary>
    /// <remarks>
    /// 用于标识 IO 点所在的硬件板卡或模块，例如 "Card0", "Module1"。
    /// 对于不需要板卡标识的系统，可以使用默认值 "Default"。
    /// </remarks>
    /// <example>Card0, Module1, PLC1</example>
    [StringLength(50, ErrorMessage = "板卡标识长度不能超过 50 个字符")]
    public string? BoardId { get; init; }

    /// <summary>
    /// 通道编号（IO 位地址）
    /// </summary>
    /// <remarks>
    /// 表示 IO 点在板卡上的物理位置，范围 0-1023。
    /// 对于雷赛控制卡，这对应输入/输出端口号。
    /// </remarks>
    [Required(ErrorMessage = "通道编号不能为空")]
    [Range(0, 1023, ErrorMessage = "通道编号必须在 0-1023 范围内")]
    public required int ChannelNumber { get; init; }

    /// <summary>
    /// IO 类型
    /// </summary>
    /// <remarks>
    /// 指定此 IO 点是输入还是输出。
    /// - Input: 输入 IO（如传感器、按钮）
    /// - Output: 输出 IO（如指示灯、继电器）
    /// </remarks>
    [Required(ErrorMessage = "IO 类型不能为空")]
    public required IoType Type { get; init; }

    /// <summary>
    /// 电平语义（高电平有效/低电平有效）
    /// </summary>
    /// <remarks>
    /// 定义 IO 点的触发/输出电平：
    /// - ActiveHigh: 高电平有效（常开按键/输出1有效）
    /// - ActiveLow: 低电平有效（常闭按键/输出0有效）
    /// </remarks>
    [Required(ErrorMessage = "电平语义不能为空")]
    public required TriggerLevel TriggerLevel { get; init; }

    /// <summary>
    /// 用途描述（可选）
    /// </summary>
    /// <remarks>
    /// 对 IO 点用途的文字描述，便于维护和理解。
    /// </remarks>
    /// <example>系统启动按钮, 格口1前置传感器, 中段皮带启用输出</example>
    [StringLength(200, ErrorMessage = "用途描述长度不能超过 200 个字符")]
    public string? Description { get; init; }

    /// <summary>
    /// 是否启用此 IO 点
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 创建一个简化的 IO 点配置（仅指定通道号和电平）
    /// </summary>
    /// <param name="name">IO 点名称</param>
    /// <param name="channelNumber">通道编号（0-1023）</param>
    /// <param name="type">IO 类型</param>
    /// <param name="triggerLevel">电平语义</param>
    /// <returns>IO 点配置实例</returns>
    public static IoPointConfiguration Create(
        string name,
        int channelNumber,
        IoType type,
        TriggerLevel triggerLevel = TriggerLevel.ActiveHigh)
    {
        return new IoPointConfiguration
        {
            Name = name,
            ChannelNumber = channelNumber,
            Type = type,
            TriggerLevel = triggerLevel
        };
    }

    /// <summary>
    /// 验证配置的有效性
    /// </summary>
    /// <returns>验证结果元组 (IsValid, ErrorMessage)</returns>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return (false, "IO 点名称不能为空");
        }

        if (ChannelNumber < 0 || ChannelNumber > 1023)
        {
            return (false, $"通道编号 {ChannelNumber} 必须在 0-1023 范围内");
        }

        if (!Enum.IsDefined(typeof(IoType), Type))
        {
            return (false, $"无效的 IO 类型: {Type}");
        }

        if (!Enum.IsDefined(typeof(TriggerLevel), TriggerLevel))
        {
            return (false, $"无效的电平语义: {TriggerLevel}");
        }

        return (true, null);
    }
}
