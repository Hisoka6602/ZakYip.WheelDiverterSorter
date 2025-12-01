using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 格口路径拓扑配置
/// </summary>
/// <remarks>
/// <para>描述从入口到各个格口的完整路径拓扑结构。</para>
/// <para>本配置通过引用其他配置中已定义的ID来组织路径关系：</para>
/// <list type="bullet">
/// <item>IO配置 - 引用 SensorConfiguration 中的 SensorId</item>
/// <item>线体段 - 通过 DiverterPathNode.SegmentId 关联</item>
/// <item>摆轮配置 - 引用 WheelDiverterConfiguration 中的 DiverterId</item>
/// </list>
/// 
/// <para><b>N 摆轮线性拓扑模型（PR-TOPO02）：</b></para>
/// <para>支持 N 个摆轮，每个摆轮左右各一个格口，末端一个异常口，总格口数 = N × 2 + 1。</para>
/// <code>
///       格口B     格口D     格口F
///         ↑         ↑         ↑
/// 入口 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 末端(异常口)
///   ↓     ↓         ↓         ↓
/// 传感器  格口A      格口C     格口E
/// </code>
/// <para>可通过 <see cref="Diverters"/> 属性配置简化的 N 摆轮模型，或使用 <see cref="DiverterNodes"/> 配置详细的路径节点。</para>
/// </remarks>
public record class ChutePathTopologyConfig
{
    /// <summary>
    /// 拓扑配置唯一标识符
    /// </summary>
    public required string TopologyId { get; init; }

    /// <summary>
    /// 拓扑配置名称
    /// </summary>
    public required string TopologyName { get; init; }

    /// <summary>
    /// 拓扑描述
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 入口传感器ID（引用 SensorConfiguration 中类型为 ParcelCreation 的传感器）
    /// </summary>
    /// <remarks>
    /// 必须引用一个已配置的 ParcelCreation 类型的感应IO
    /// </remarks>
    public required long EntrySensorId { get; init; }

    /// <summary>
    /// 摆轮路径节点列表（按物理位置顺序排列）
    /// </summary>
    /// <remarks>
    /// 每个节点描述一个摆轮及其关联的格口和线体段
    /// </remarks>
    public required IReadOnlyList<DiverterPathNode> DiverterNodes { get; init; }

    /// <summary>
    /// 末端异常格口ID
    /// </summary>
    /// <remarks>
    /// 当包裹无法分拣到任何目标格口时，将被导向此异常格口
    /// </remarks>
    public required long ExceptionChuteId { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 根据摆轮ID查找路径节点
    /// </summary>
    /// <param name="diverterId">摆轮ID</param>
    /// <returns>匹配的路径节点，如不存在则返回null</returns>
    public DiverterPathNode? FindNodeByDiverterId(long diverterId)
    {
        return DiverterNodes.FirstOrDefault(n => n.DiverterId == diverterId);
    }

    /// <summary>
    /// 根据格口ID查找对应的摆轮路径节点
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <returns>包含该格口的路径节点，如不存在则返回null</returns>
    public DiverterPathNode? FindNodeByChuteId(long chuteId)
    {
        return DiverterNodes.FirstOrDefault(n => 
            n.LeftChuteIds.Contains(chuteId) || 
            n.RightChuteIds.Contains(chuteId));
    }

    /// <summary>
    /// 获取到达指定格口需要经过的所有摆轮节点
    /// </summary>
    /// <param name="chuteId">目标格口ID</param>
    /// <returns>从入口到目标格口的摆轮路径，如不存在则返回null</returns>
    public IReadOnlyList<DiverterPathNode>? GetPathToChute(long chuteId)
    {
        var targetNode = FindNodeByChuteId(chuteId);
        if (targetNode == null)
        {
            return null;
        }

        // 返回从入口到目标摆轮的所有节点（按位置索引排序）
        return DiverterNodes
            .Where(n => n.PositionIndex <= targetNode.PositionIndex)
            .OrderBy(n => n.PositionIndex)
            .ToList();
    }

    /// <summary>
    /// 获取指定格口的分拣方向
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <returns>分拣方向（Left/Right），如格口不存在则返回null</returns>
    public DiverterDirection? GetChuteDirection(long chuteId)
    {
        var node = FindNodeByChuteId(chuteId);
        if (node == null)
        {
            return null;
        }

        if (node.LeftChuteIds.Contains(chuteId))
        {
            return DiverterDirection.Left;
        }
        
        if (node.RightChuteIds.Contains(chuteId))
        {
            return DiverterDirection.Right;
        }

        return null;
    }

    /// <summary>
    /// 获取所有格口的总数
    /// </summary>
    public int TotalChuteCount => DiverterNodes.Sum(n => n.LeftChuteIds.Count + n.RightChuteIds.Count);

    /// <summary>
    /// 简化的 N 摆轮配置列表（PR-TOPO02）
    /// </summary>
    /// <remarks>
    /// <para>用于简化的 N 摆轮模型，每个摆轮仅有左右各一个格口。</para>
    /// <para>如果设置此属性，可通过 <see cref="ChutePathTopologyValidator"/> 验证配置是否符合 N 摆轮模型约束。</para>
    /// <para>此属性与 <see cref="DiverterNodes"/> 互补，简化模型使用此属性，复杂模型使用 DiverterNodes。</para>
    /// </remarks>
    public IReadOnlyList<DiverterNodeConfig>? Diverters { get; init; }

    /// <summary>
    /// 末端异常格口ID（别名，与 ExceptionChuteId 等价）
    /// </summary>
    /// <remarks>
    /// <para>用于 N 摆轮简化模型的异常格口配置。</para>
    /// <para>当包裹无法分拣到任何目标格口时，所有摆轮设为直通，包裹落入末端异常口。</para>
    /// </remarks>
    public long AbnormalChuteId => ExceptionChuteId;

    /// <summary>
    /// 从简化的 Diverters 配置生成 DiverterNodes
    /// </summary>
    /// <returns>生成的 DiverterPathNode 列表</returns>
    /// <remarks>
    /// 当使用简化的 N 摆轮模型时，可通过此方法将 <see cref="Diverters"/> 转换为 <see cref="DiverterNodes"/> 格式
    /// </remarks>
    public IReadOnlyList<DiverterPathNode> GenerateNodesFromDiverters()
    {
        if (Diverters == null || Diverters.Count == 0)
        {
            return DiverterNodes;
        }

        return Diverters.Select(d => new DiverterPathNode
        {
            DiverterId = d.Index,
            DiverterName = $"摆轮D{d.Index}",
            PositionIndex = d.Index,
            SegmentId = d.Index,
            FrontSensorId = d.Index + 100, // 默认传感器ID偏移
            LeftChuteIds = new[] { d.LeftChuteId },
            RightChuteIds = new[] { d.RightChuteId }
        }).ToList();
    }
}

/// <summary>
/// 摆轮路径节点
/// </summary>
/// <remarks>
/// <para>描述单个摆轮在路径中的配置，包括：</para>
/// <list type="bullet">
/// <item>摆轮ID - 引用已配置的摆轮设备</item>
/// <item>前置线体段ID - 到达此摆轮需要经过的线体段</item>
/// <item>左右侧格口ID - 此摆轮左转/右转对应的格口</item>
/// <item>摆轮前感应IO ID - 可选，用于检测包裹即将到达摆轮</item>
/// </list>
/// </remarks>
public record class DiverterPathNode
{
    /// <summary>
    /// 摆轮ID（引用 WheelDiverterConfiguration 中的摆轮设备）
    /// </summary>
    public required long DiverterId { get; init; }

    /// <summary>
    /// 摆轮显示名称
    /// </summary>
    public string? DiverterName { get; init; }

    /// <summary>
    /// 物理位置索引（从入口开始的顺序，从1开始）
    /// </summary>
    public required int PositionIndex { get; init; }

    /// <summary>
    /// 前置线体段ID
    /// </summary>
    /// <remarks>
    /// 从上一个节点（入口或上一个摆轮）到本摆轮的线体段
    /// </remarks>
    public required long SegmentId { get; init; }

    /// <summary>
    /// 摆轮前感应IO的ID（引用 SensorConfiguration 中的 SensorId，必须配置）
    /// </summary>
    /// <remarks>
    /// 类型必须为 WheelFront，用于检测包裹是否已经到达摆轮前。
    /// 此字段为必填项，因为需要依靠感应器来判断包裹是否已经到达摆轮前。
    /// </remarks>
    public required long FrontSensorId { get; init; }

    /// <summary>
    /// 左侧格口ID列表
    /// </summary>
    /// <remarks>
    /// 摆轮左转时可分拣到的格口
    /// </remarks>
    public IReadOnlyList<long> LeftChuteIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 右侧格口ID列表
    /// </summary>
    /// <remarks>
    /// 摆轮右转时可分拣到的格口
    /// </remarks>
    public IReadOnlyList<long> RightChuteIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 备注信息
    /// </summary>
    public string? Remarks { get; init; }

    /// <summary>
    /// 左侧是否有格口
    /// </summary>
    public bool HasLeftChute => LeftChuteIds.Count > 0;

    /// <summary>
    /// 右侧是否有格口
    /// </summary>
    public bool HasRightChute => RightChuteIds.Count > 0;
}

/// <summary>
/// 简化的摆轮节点配置（PR-TOPO02）
/// </summary>
/// <remarks>
/// <para>用于 N 摆轮线性拓扑模型，每个摆轮左右各一个格口。</para>
/// <para>总格口数 = N × 2 + 1（末端异常口）</para>
/// </remarks>
public readonly record struct DiverterNodeConfig
{
    /// <summary>
    /// 摆轮索引（从 1 开始）
    /// </summary>
    /// <example>1, 2, 3</example>
    public required int Index { get; init; }

    /// <summary>
    /// 左侧格口ID
    /// </summary>
    /// <remarks>
    /// 摆轮左转时分拣到的格口
    /// </remarks>
    public required long LeftChuteId { get; init; }

    /// <summary>
    /// 右侧格口ID
    /// </summary>
    /// <remarks>
    /// 摆轮右转时分拣到的格口
    /// </remarks>
    public required long RightChuteId { get; init; }
}

/// <summary>
/// 格口路径拓扑配置验证器（PR-TOPO02）
/// </summary>
/// <remarks>
/// <para>验证简化的 N 摆轮模型配置是否符合约束：</para>
/// <list type="bullet">
/// <item>至少一个摆轮：Diverters.Count >= 1</item>
/// <item>格口数量 = 摆轮数量 × 2</item>
/// <item>异常格口不在普通格口集合中</item>
/// <item>所有格口ID全局唯一</item>
/// <item>总格口数（含异常口）= 摆轮数量 × 2 + 1</item>
/// </list>
/// </remarks>
public static class ChutePathTopologyValidator
{
    /// <summary>
    /// 验证简化的 N 摆轮拓扑配置
    /// </summary>
    /// <param name="diverters">摆轮配置列表</param>
    /// <param name="abnormalChuteId">异常格口ID</param>
    /// <returns>验证结果：(是否有效, 错误消息)</returns>
    public static (bool IsValid, string? ErrorMessage) ValidateNDiverterTopology(
        IReadOnlyList<DiverterNodeConfig> diverters,
        long abnormalChuteId)
    {
        // 验证至少一个摆轮
        if (diverters == null || diverters.Count < 1)
        {
            return (false, "至少需要配置一个摆轮 - At least one diverter is required");
        }

        // 收集所有格口ID
        var allChutes = new HashSet<long>();
        foreach (var diverter in diverters)
        {
            if (!allChutes.Add(diverter.LeftChuteId))
            {
                return (false, $"格口ID {diverter.LeftChuteId} 重复 - Duplicate chute ID {diverter.LeftChuteId}");
            }
            if (!allChutes.Add(diverter.RightChuteId))
            {
                return (false, $"格口ID {diverter.RightChuteId} 重复 - Duplicate chute ID {diverter.RightChuteId}");
            }
        }

        // 验证格口数量 = 摆轮数量 × 2
        var expectedChuteCount = diverters.Count * 2;
        if (allChutes.Count != expectedChuteCount)
        {
            return (false, $"格口数量 ({allChutes.Count}) 不等于摆轮数量 × 2 ({expectedChuteCount}) - Chute count mismatch");
        }

        // 验证异常格口不在普通格口集合中
        if (allChutes.Contains(abnormalChuteId))
        {
            return (false, $"异常格口ID ({abnormalChuteId}) 不能与普通格口重复 - Abnormal chute ID cannot duplicate with normal chutes");
        }

        // 验证总格口数（含异常口）= 摆轮数量 × 2 + 1
        var totalChuteCount = allChutes.Count + 1; // +1 for abnormal chute
        var expectedTotalCount = diverters.Count * 2 + 1;
        if (totalChuteCount != expectedTotalCount)
        {
            return (false, $"总格口数 ({totalChuteCount}) 不等于 N × 2 + 1 ({expectedTotalCount}) - Total chute count should be N × 2 + 1");
        }

        // 验证摆轮索引从1开始连续
        var indices = diverters.Select(d => d.Index).OrderBy(i => i).ToList();
        for (int i = 0; i < indices.Count; i++)
        {
            if (indices[i] != i + 1)
            {
                return (false, $"摆轮索引应从1开始连续，发现索引 {indices[i]} 不符合要求 - Diverter index should start from 1 and be consecutive");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 验证 ChutePathTopologyConfig 中的简化 N 摆轮配置
    /// </summary>
    /// <param name="config">拓扑配置</param>
    /// <returns>验证结果：(是否有效, 错误消息)</returns>
    public static (bool IsValid, string? ErrorMessage) Validate(ChutePathTopologyConfig config)
    {
        if (config.Diverters != null && config.Diverters.Count > 0)
        {
            // 使用简化模型验证
            return ValidateNDiverterTopology(config.Diverters, config.AbnormalChuteId);
        }

        // 使用详细模型验证（DiverterNodes）
        if (config.DiverterNodes == null || config.DiverterNodes.Count < 1)
        {
            return (false, "至少需要配置一个摆轮节点 - At least one diverter node is required");
        }

        // 验证所有格口ID唯一
        var allChuteIds = new HashSet<long>();
        foreach (var node in config.DiverterNodes)
        {
            foreach (var chuteId in node.LeftChuteIds)
            {
                if (!allChuteIds.Add(chuteId))
                {
                    return (false, $"格口ID {chuteId} 重复 - Duplicate chute ID {chuteId}");
                }
            }
            foreach (var chuteId in node.RightChuteIds)
            {
                if (!allChuteIds.Add(chuteId))
                {
                    return (false, $"格口ID {chuteId} 重复 - Duplicate chute ID {chuteId}");
                }
            }
        }

        // 验证异常格口不在普通格口集合中
        if (allChuteIds.Contains(config.ExceptionChuteId))
        {
            return (false, $"异常格口ID ({config.ExceptionChuteId}) 不能与普通格口重复 - Exception chute ID cannot duplicate with normal chutes");
        }

        return (true, null);
    }
}
