namespace ZakYip.WheelDiverterSorter.Drivers;

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
