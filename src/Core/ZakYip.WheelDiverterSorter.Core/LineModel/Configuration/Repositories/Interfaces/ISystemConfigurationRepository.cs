using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

/// <summary>
/// 系统配置仓储接口
/// </summary>
public interface ISystemConfigurationRepository
{
    /// <summary>
    /// 获取系统配置
    /// </summary>
    /// <returns>系统配置，如不存在则返回默认配置</returns>
    SystemConfiguration Get();

    /// <summary>
    /// 更新系统配置
    /// </summary>
    /// <param name="configuration">系统配置</param>
    void Update(SystemConfiguration configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    /// <param name="currentTime">当前本地时间（可选，用于设置 CreatedAt 和 UpdatedAt）</param>
    /// <remarks>
    /// 如果数据库中没有配置，则插入默认配置
    /// </remarks>
    void InitializeDefault(DateTime? currentTime = null);
}
