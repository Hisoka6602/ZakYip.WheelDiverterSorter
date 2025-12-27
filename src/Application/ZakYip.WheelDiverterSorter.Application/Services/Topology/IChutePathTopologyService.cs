using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using CoreConfig = ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

namespace ZakYip.WheelDiverterSorter.Application.Services.Topology;

/// <summary>
/// Application 层格口路径拓扑服务接口（扩展）
/// </summary>
/// <remarks>
/// 提供格口路径拓扑配置的管理功能，包括获取、更新、验证等操作。
/// Host 层的 Controller 应通过此服务访问拓扑配置，
/// 而不是直接依赖 Core 层的仓储接口。
/// 继承 Core 层的 IChutePathTopologyService 接口，提供配置更新和高级操作。
/// </remarks>
public interface IChutePathTopologyService : CoreConfig.IChutePathTopologyService
{
    // GetTopology() inherited from CoreConfig.IChutePathTopologyService

    /// <summary>
    /// 更新格口路径拓扑配置
    /// </summary>
    /// <param name="config">新的配置</param>
    void UpdateTopology(ChutePathTopologyConfig config);

    /// <summary>
    /// 验证拓扑配置请求
    /// </summary>
    /// <param name="entrySensorId">入口传感器ID</param>
    /// <param name="diverterNodes">摆轮节点列表</param>
    /// <param name="exceptionChuteId">异常格口ID</param>
    /// <returns>验证结果，如果验证通过返回 (true, null)，否则返回 (false, 错误消息)</returns>
    (bool IsValid, string? ErrorMessage) ValidateTopologyRequest(
        long entrySensorId,
        IReadOnlyList<DiverterPathNode> diverterNodes,
        long exceptionChuteId);

    /// <summary>
    /// 验证简化的 N 摆轮拓扑配置（PR-TOPO02）
    /// </summary>
    /// <param name="diverters">简化的摆轮配置列表</param>
    /// <param name="abnormalChuteId">异常格口ID</param>
    /// <returns>验证结果，如果验证通过返回 (true, null)，否则返回 (false, 错误消息)</returns>
    (bool IsValid, string? ErrorMessage) ValidateNDiverterTopology(
        IReadOnlyList<DiverterNodeConfig> diverters,
        long abnormalChuteId);

    /// <summary>
    /// 根据格口ID查找对应的摆轮节点
    /// </summary>
    /// <param name="chuteId">格口ID</param>
    /// <returns>找到的摆轮节点，如果未找到返回 null</returns>
    DiverterPathNode? FindNodeByChuteId(long chuteId);

    /// <summary>
    /// 获取到达指定格口的路径节点列表
    /// </summary>
    /// <param name="chuteId">目标格口ID</param>
    /// <returns>从入口到目标格口经过的摆轮节点列表</returns>
    IReadOnlyList<DiverterPathNode>? GetPathToChute(long chuteId);

    /// <summary>
    /// 为包裹创建摆轮路径（PR-TOPO02）
    /// </summary>
    /// <param name="targetChuteId">目标格口ID</param>
    /// <returns>生成的摆轮路径，如果无法生成则返回 null</returns>
    /// <remarks>
    /// 此方法内部调用 ISwitchingPathGenerator.GeneratePath(chuteId)，
    /// 上层（如 SortingOrchestrator）完全不关心当前系统有几个摆轮，只依赖此服务。
    /// </remarks>
    SwitchingPath? CreatePathForParcel(long targetChuteId);
}
