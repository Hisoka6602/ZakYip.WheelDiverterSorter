namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// 面板配置仓储接口
/// </summary>
public interface IPanelConfigurationRepository
{
    /// <summary>
    /// 获取面板配置
    /// </summary>
    /// <returns>面板配置，如不存在则返回默认配置</returns>
    PanelConfiguration Get();

    /// <summary>
    /// 更新面板配置
    /// </summary>
    /// <param name="configuration">面板配置</param>
    void Update(PanelConfiguration configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    /// <param name="currentTime">当前本地时间（可选，用于设置 CreatedAt 和 UpdatedAt）</param>
    /// <remarks>
    /// 如果数据库中没有配置，则插入默认配置
    /// </remarks>
    void InitializeDefault(DateTime? currentTime = null);
}
