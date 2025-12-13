using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

/// <summary>
/// 落格回调配置仓储接口
/// </summary>
public interface IChuteDropoffCallbackConfigurationRepository
{
    /// <summary>
    /// 获取落格回调配置
    /// </summary>
    /// <returns>落格回调配置，如不存在则返回默认配置</returns>
    ChuteDropoffCallbackConfiguration Get();

    /// <summary>
    /// 更新落格回调配置
    /// </summary>
    /// <param name="configuration">落格回调配置</param>
    void Update(ChuteDropoffCallbackConfiguration configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    /// <param name="currentTime">当前本地时间（可选，用于设置 CreatedAt 和 UpdatedAt）</param>
    /// <remarks>
    /// 如果数据库中没有配置，则插入默认配置
    /// </remarks>
    void InitializeDefault(DateTime? currentTime = null);
}
