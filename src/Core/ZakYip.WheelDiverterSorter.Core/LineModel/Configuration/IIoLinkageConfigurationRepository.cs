namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

/// <summary>
/// IO 联动配置仓储接口
/// </summary>
/// <remarks>
/// 用于持久化和管理 IO 联动配置，支持热更新。
/// IO 联动用于根据系统运行状态自动控制外部设备的 IO 点位。
/// </remarks>
public interface IIoLinkageConfigurationRepository
{
    /// <summary>
    /// 获取 IO 联动配置
    /// </summary>
    /// <returns>IO 联动配置，如不存在则返回默认配置</returns>
    IoLinkageConfiguration Get();

    /// <summary>
    /// 更新 IO 联动配置
    /// </summary>
    /// <param name="configuration">IO 联动配置</param>
    void Update(IoLinkageConfiguration configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    /// <param name="currentTime">当前本地时间（可选，用于设置 CreatedAt 和 UpdatedAt）</param>
    /// <remarks>
    /// 如果数据库中没有配置，则插入默认配置
    /// </remarks>
    void InitializeDefault(DateTime? currentTime = null);
}
