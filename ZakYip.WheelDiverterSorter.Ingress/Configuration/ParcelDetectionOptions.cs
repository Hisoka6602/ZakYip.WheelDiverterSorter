namespace ZakYip.WheelDiverterSorter.Ingress.Configuration;

/// <summary>
/// 包裹检测配置选项
/// </summary>
public class ParcelDetectionOptions
{
    /// <summary>
    /// 去重时间窗口（毫秒）
    /// 在此时间窗口内，同一传感器的重复触发将被忽略
    /// 默认值: 1000ms (1秒)
    /// </summary>
    public int DeduplicationWindowMs { get; set; } = 1000;

    /// <summary>
    /// 包裹ID历史记录最大数量
    /// 用于防止重复包裹ID，超过此数量将移除最旧的记录
    /// 默认值: 1000
    /// </summary>
    public int ParcelIdHistorySize { get; set; } = 1000;
}
