namespace ZakYip.WheelDiverterSorter.Drivers;

/// <summary>
/// 驱动器配置选项
/// </summary>
public class DriverOptions
{
    /// <summary>
    /// 是否使用硬件驱动器（false则使用模拟驱动器）
    /// </summary>
    public bool UseHardwareDriver { get; set; } = false;

    /// <summary>
    /// 雷赛控制器配置
    /// </summary>
    public LeadshineOptions Leadshine { get; set; } = new();
}

/// <summary>
/// 雷赛控制器配置选项
/// </summary>
public class LeadshineOptions
{
    /// <summary>
    /// 控制器卡号
    /// </summary>
    public ushort CardNo { get; set; } = 0;

    /// <summary>
    /// 摆轮配置列表
    /// </summary>
    public List<LeadshineDiverterConfigDto> Diverters { get; set; } = new();
}

/// <summary>
/// 摆轮配置DTO
/// </summary>
public class LeadshineDiverterConfigDto
{
    /// <summary>
    /// 摆轮ID
    /// </summary>
    public required string DiverterId { get; set; }

    /// <summary>
    /// 输出起始位
    /// </summary>
    public required int OutputStartBit { get; set; }

    /// <summary>
    /// 反馈输入位（可选）
    /// </summary>
    public int? FeedbackInputBit { get; set; }
}
