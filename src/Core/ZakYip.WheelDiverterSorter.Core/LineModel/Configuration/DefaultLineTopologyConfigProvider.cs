using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 默认线体拓扑配置提供者
/// </summary>
/// <remarks>
/// 提供内存中的默认拓扑配置，与原 DefaultSorterTopologyProvider 兼容
/// </remarks>
public class DefaultLineTopologyConfigProvider : ILineTopologyConfigProvider
{
    private readonly LineTopologyConfig _defaultConfig;

    public DefaultLineTopologyConfigProvider()
    {
        _defaultConfig = CreateDefaultTopology();
    }

    /// <inheritdoc />
    public Task<LineTopologyConfig> GetTopologyAsync()
    {
        return Task.FromResult(_defaultConfig);
    }

    /// <inheritdoc />
    public Task RefreshAsync()
    {
        // 默认配置不需要刷新
        return Task.CompletedTask;
    }

    /// <summary>
    /// 创建默认拓扑配置
    /// </summary>
    /// <remarks>
    /// 与 DefaultSorterTopologyProvider.GetDefaultTopology() 保持一致的拓扑结构
    /// </remarks>
    private static LineTopologyConfig CreateDefaultTopology()
    {
        var wheelNodes = new List<WheelNodeConfig>
        {
            // 节点A: 第一个摆轮，支持直行和左转
            new WheelNodeConfig
            {
                NodeId = "DIVERTER_A",
                NodeName = "摆轮节点A",
                PositionIndex = 0,
                HasLeftChute = true,
                HasRightChute = false,
                LeftChuteIds = new[] { "CHUTE_A1", "CHUTE_A2" },
                RightChuteIds = Array.Empty<string>(),
                SupportedSides = new[] { DiverterSide.Straight, DiverterSide.Left }
            },
            // 节点B: 第二个摆轮，支持直行和右转
            new WheelNodeConfig
            {
                NodeId = "DIVERTER_B",
                NodeName = "摆轮节点B",
                PositionIndex = 1,
                HasLeftChute = false,
                HasRightChute = true,
                LeftChuteIds = Array.Empty<string>(),
                RightChuteIds = new[] { "CHUTE_B1" },
                SupportedSides = new[] { DiverterSide.Straight, DiverterSide.Right }
            },
            // 节点C: 第三个摆轮，支持直行、左转和右转
            new WheelNodeConfig
            {
                NodeId = "DIVERTER_C",
                NodeName = "摆轮节点C",
                PositionIndex = 2,
                HasLeftChute = true,
                HasRightChute = true,
                LeftChuteIds = new[] { "CHUTE_C1" },
                RightChuteIds = new[] { "CHUTE_C2", "CHUTE_C3" },
                SupportedSides = new[] { DiverterSide.Straight, DiverterSide.Left, DiverterSide.Right }
            }
        };

        var chutes = new List<ChuteConfig>
        {
            new ChuteConfig { ChuteId = "CHUTE_A1", ChuteName = "格口A1", BoundNodeId = "DIVERTER_A", BoundDirection = "Left", IsExceptionChute = false },
            new ChuteConfig { ChuteId = "CHUTE_A2", ChuteName = "格口A2", BoundNodeId = "DIVERTER_A", BoundDirection = "Left", IsExceptionChute = false },
            new ChuteConfig { ChuteId = "CHUTE_B1", ChuteName = "格口B1", BoundNodeId = "DIVERTER_B", BoundDirection = "Right", IsExceptionChute = false },
            new ChuteConfig { ChuteId = "CHUTE_C1", ChuteName = "格口C1", BoundNodeId = "DIVERTER_C", BoundDirection = "Left", IsExceptionChute = false },
            new ChuteConfig { ChuteId = "CHUTE_C2", ChuteName = "格口C2", BoundNodeId = "DIVERTER_C", BoundDirection = "Right", IsExceptionChute = false },
            new ChuteConfig { ChuteId = "CHUTE_C3", ChuteName = "格口C3", BoundNodeId = "DIVERTER_C", BoundDirection = "Right", IsExceptionChute = false },
            new ChuteConfig { ChuteId = "CHUTE_END", ChuteName = "末端/异常格口", BoundNodeId = "DIVERTER_C", BoundDirection = "Straight", IsExceptionChute = true }
        };

        return new LineTopologyConfig
        {
            TopologyId = "DEFAULT_LINEAR_TOPOLOGY",
            TopologyName = "默认直线摆轮分拣拓扑",
            Description = "默认直线摆轮分拣拓扑 - 示例配置",
            WheelNodes = wheelNodes,
            Chutes = chutes,
            DefaultLineSpeedMmps = 500m
        };
    }
}
