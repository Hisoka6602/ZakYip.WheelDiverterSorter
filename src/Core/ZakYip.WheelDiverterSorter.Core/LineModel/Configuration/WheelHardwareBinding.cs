namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 摆轮硬件绑定配置
/// </summary>
/// <remarks>
/// 将逻辑摆轮与物理IO/驱动绑定
/// </remarks>
public record class WheelHardwareBinding
{
    /// <summary>
    /// 摆轮节点ID（逻辑标识）
    /// </summary>
    public required string WheelNodeId { get; init; }

    /// <summary>
    /// 摆轮显示名称
    /// </summary>
    public required string WheelName { get; init; }

    /// <summary>
    /// 驱动器ID（数字ID）
    /// </summary>
    /// <remarks>
    /// 与硬件驱动配置中的摆轮ID对应
    /// </remarks>
    public required int DriverId { get; init; }

    /// <summary>
    /// 驱动器名称（可选）
    /// </summary>
    public string? DriverName { get; init; }

    /// <summary>
    /// IO板地址（可选）
    /// </summary>
    /// <remarks>
    /// 例如：192.168.1.100
    /// </remarks>
    public string? IoAddress { get; init; }

    /// <summary>
    /// IO通道号（可选）
    /// </summary>
    public int? IoChannel { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }
}

/// <summary>
/// 摆轮硬件绑定配置集合
/// </summary>
public record class WheelBindingsConfig
{
    /// <summary>
    /// 配置ID（LiteDB自动生成）
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 配置名称
    /// </summary>
    public string ConfigName { get; set; } = "wheel-bindings";

    /// <summary>
    /// 摆轮绑定列表
    /// </summary>
    public required List<WheelHardwareBinding> Bindings { get; init; }

    /// <summary>
    /// 创建时间（本地时间）
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间（本地时间）
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 摆轮硬件绑定配置仓储接口
/// </summary>
public interface IWheelBindingsRepository
{
    /// <summary>
    /// 获取摆轮硬件绑定配置
    /// </summary>
    /// <returns>摆轮硬件绑定配置，如不存在则返回默认配置</returns>
    WheelBindingsConfig Get();

    /// <summary>
    /// 更新摆轮硬件绑定配置
    /// </summary>
    /// <param name="configuration">摆轮硬件绑定配置</param>
    void Update(WheelBindingsConfig configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    /// <param name="currentTime">当前本地时间（可选，用于设置 CreatedAt 和 UpdatedAt）</param>
    void InitializeDefault(DateTime? currentTime = null);
}
