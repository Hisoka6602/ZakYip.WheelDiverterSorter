namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 驱动器配置（存储在LiteDB中，支持热更新）
/// </summary>
public class DriverConfiguration
{
    /// <summary>
    /// LiteDB自动生成的唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 是否使用硬件驱动器（false则使用模拟驱动器）
    /// </summary>
    public bool UseHardwareDriver { get; set; } = false;

    /// <summary>
    /// 驱动器厂商类型（枚举值：Mock=0, Leadshine=1, Siemens=2, Mitsubishi=3, Omron=4）
    /// </summary>
    public int VendorType { get; set; } = 1; // Leadshine

    /// <summary>
    /// 雷赛控制器配置
    /// </summary>
    public LeadshineDriverConfig? Leadshine { get; set; }

    /// <summary>
    /// 配置版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 配置创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 配置最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static DriverConfiguration GetDefault()
    {
        return new DriverConfiguration
        {
            UseHardwareDriver = false,
            VendorType = 1, // Leadshine
            Leadshine = new LeadshineDriverConfig
            {
                CardNo = 0,
                Diverters = new List<DiverterDriverEntry>
                {
                    new() { DiverterId = 1, DiverterName = "D1", OutputStartBit = 0, FeedbackInputBit = 10 },
                    new() { DiverterId = 2, DiverterName = "D2", OutputStartBit = 2, FeedbackInputBit = 11 },
                    new() { DiverterId = 3, DiverterName = "D3", OutputStartBit = 4, FeedbackInputBit = 12 }
                }
            }
        };
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (VendorType < 0 || VendorType > 4)
        {
            return (false, "驱动器厂商类型无效");
        }

        if (UseHardwareDriver && VendorType == 1 && Leadshine == null)
        {
            return (false, "使用雷赛硬件驱动时，必须配置雷赛参数");
        }

        if (Leadshine != null)
        {
            if (Leadshine.Diverters == null || !Leadshine.Diverters.Any())
            {
                return (false, "雷赛摆轮配置不能为空");
            }

            // 检查DiverterId不能重复
            var duplicateIds = Leadshine.Diverters
                .GroupBy(d => d.DiverterId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateIds.Any())
            {
                return (false, $"摆轮ID重复: {string.Join(", ", duplicateIds)}");
            }
        }

        return (true, null);
    }
}

/// <summary>
/// 雷赛控制器配置
/// </summary>
public class LeadshineDriverConfig
{
    /// <summary>
    /// 控制器卡号
    /// </summary>
    public ushort CardNo { get; set; } = 0;

    /// <summary>
    /// 摆轮配置列表
    /// </summary>
    public List<DiverterDriverEntry> Diverters { get; set; } = new();
}

/// <summary>
/// 摆轮驱动器配置条目
/// </summary>
public class DiverterDriverEntry
{
    /// <summary>
    /// 摆轮标识符（数字ID）
    /// </summary>
    public required int DiverterId { get; set; }

    /// <summary>
    /// 摆轮名称（可选）- Diverter Name (Optional)
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "D1"、"1号摆轮"
    /// </remarks>
    public string? DiverterName { get; set; }

    /// <summary>
    /// 输出起始位
    /// </summary>
    public int OutputStartBit { get; set; }

    /// <summary>
    /// 反馈输入位
    /// </summary>
    public int FeedbackInputBit { get; set; }
}
