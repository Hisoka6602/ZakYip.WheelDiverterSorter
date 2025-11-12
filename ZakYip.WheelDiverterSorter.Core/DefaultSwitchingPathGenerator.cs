namespace ZakYip.WheelDiverterSorter.Core;

/// <summary>
/// 默认的摆轮路径生成器实现，基于直线摆轮方案
/// <para>
/// 实现逻辑：包裹进入 → 获取目标Chute → 生成从入口到该Chute的摆轮列表
/// </para>
/// </summary>
public class DefaultSwitchingPathGenerator : ISwitchingPathGenerator
{
    /// <summary>
    /// 默认的段TTL值（毫秒）
    /// </summary>
    /// <remarks>
    /// 当前使用固定值，后续可通过配置或策略进行动态设置
    /// </remarks>
    private const int DefaultSegmentTtlMs = 5000;

    // TODO: 这里应该注入或配置格口到摆轮的映射关系
    // 当前是示例性的硬编码映射，实际项目中应从配置文件或数据库加载
    private readonly Dictionary<string, List<DiverterConfig>> _chuteToRouteMap;

    public DefaultSwitchingPathGenerator()
    {
        // 示例映射：格口 -> 摆轮配置列表
        // 实际生产环境应通过构造函数注入或配置加载
        _chuteToRouteMap = new Dictionary<string, List<DiverterConfig>>
        {
            // 示例：格口A需要经过摆轮D1（30度）和摆轮D2（45度）
            ["CHUTE_A"] = new List<DiverterConfig>
            {
                new DiverterConfig("D1", DiverterAngle.Angle30),
                new DiverterConfig("D2", DiverterAngle.Angle45)
            },
            // 示例：格口B需要经过摆轮D1（0度直行）
            ["CHUTE_B"] = new List<DiverterConfig>
            {
                new DiverterConfig("D1", DiverterAngle.Angle0)
            },
            // 示例：格口C需要经过摆轮D1（90度）和摆轮D3（30度）
            ["CHUTE_C"] = new List<DiverterConfig>
            {
                new DiverterConfig("D1", DiverterAngle.Angle90),
                new DiverterConfig("D3", DiverterAngle.Angle30)
            }
        };
    }

    /// <summary>
    /// 根据目标格口生成摆轮路径
    /// </summary>
    /// <param name="targetChuteId">目标格口标识</param>
    /// <returns>
    /// 生成的摆轮路径，如果目标格口无法映射到任意摆轮组合则返回null。
    /// 当返回null时，包裹将走异常口处理流程。
    /// </returns>
    public SwitchingPath? GeneratePath(string targetChuteId)
    {
        if (string.IsNullOrWhiteSpace(targetChuteId))
        {
            return null;
        }

        // 查找格口对应的摆轮配置
        if (!_chuteToRouteMap.TryGetValue(targetChuteId, out var diverterConfigs))
        {
            // 无法映射到摆轮组合，返回null，包裹将走异常口
            return null;
        }

        // 生成有序的摆轮段列表，顺序号从1开始
        var segments = diverterConfigs
            .Select((config, index) => new SwitchingPathSegment
            {
                SequenceNumber = index + 1,
                DiverterId = config.DiverterId,
                TargetAngle = config.TargetAngle,
                TtlMilliseconds = DefaultSegmentTtlMs
            })
            .ToList();

        return new SwitchingPath
        {
            TargetChuteId = targetChuteId,
            Segments = segments.AsReadOnly(),
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// 内部配置类，表示单个摆轮的配置
    /// </summary>
    private record DiverterConfig(string DiverterId, DiverterAngle TargetAngle);
}
