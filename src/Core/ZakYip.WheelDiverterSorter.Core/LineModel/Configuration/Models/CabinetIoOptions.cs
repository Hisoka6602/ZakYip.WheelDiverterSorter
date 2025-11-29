using System.ComponentModel.DataAnnotations;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 厂商无关的控制面板 IO 配置选项。
/// </summary>
/// <remarks>
/// 此类定义了控制面板 IO 的通用配置，不包含厂商特定的实现细节。
/// 具体厂商的扩展配置应该在 Drivers/Vendors/[VendorName]/Configuration 中定义。
/// </remarks>
public sealed record class CabinetIoOptions
{
    /// <summary>是否启用物理按键。</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>轮询间隔（毫秒），默认 50ms。</summary>
    [Range(10, 10000, ErrorMessage = "轮询间隔必须在 10 到 10000 毫秒之间")]
    public int PollingIntervalMs { get; init; } = 50;

    /// <summary>控制面板输入点位配置（控制按钮）。</summary>
    public CabinetInputPoint CabinetInputPoint { get; init; } = new();

    /// <summary>控制面板指示灯点位配置。</summary>
    public CabinetIndicatorPoint CabinetIndicatorPoint { get; init; } = new();

    /// <summary>
    /// 厂商配置键/标识。
    /// </summary>
    /// <remarks>
    /// 用于关联厂商特定的扩展配置。
    /// 可选值如: "Leadshine", "Siemens", "Simulated" 等。
    /// 实际的厂商配置由 Drivers 层的配置提供者负责解析。
    /// </remarks>
    public string VendorProfileKey { get; init; } = "Leadshine";
}
