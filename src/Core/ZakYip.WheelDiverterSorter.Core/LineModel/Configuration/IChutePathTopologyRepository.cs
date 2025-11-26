namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 格口路径拓扑配置仓储接口
/// </summary>
public interface IChutePathTopologyRepository
{
    /// <summary>
    /// 获取格口路径拓扑配置
    /// </summary>
    /// <returns>格口路径拓扑配置，如不存在则返回默认配置</returns>
    ChutePathTopologyConfig Get();

    /// <summary>
    /// 更新格口路径拓扑配置
    /// </summary>
    /// <param name="configuration">格口路径拓扑配置</param>
    void Update(ChutePathTopologyConfig configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    /// <param name="currentTime">当前本地时间（可选，用于设置 CreatedAt 和 UpdatedAt）</param>
    /// <remarks>
    /// 如果数据库中没有配置，则插入默认配置
    /// </remarks>
    void InitializeDefault(DateTime? currentTime = null);
}
