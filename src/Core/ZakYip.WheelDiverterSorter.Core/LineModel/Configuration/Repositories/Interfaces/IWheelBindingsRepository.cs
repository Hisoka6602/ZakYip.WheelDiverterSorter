using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

/// <summary>
/// 摆轮硬件绑定配置仓储接口
/// </summary>
public interface IWheelBindingsRepository
{
    /// <summary>
    /// 获取摆轮硬件绑定配置
    /// </summary>
    /// <returns>摆轮硬件绑定配置，如不存在则返回默认配置</returns>
    WheelBindingsConfig Get();

    /// <summary>
    /// 更新摆轮硬件绑定配置
    /// </summary>
    /// <param name="configuration">摆轮硬件绑定配置</param>
    void Update(WheelBindingsConfig configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    /// <param name="currentTime">当前本地时间（可选，用于设置 CreatedAt 和 UpdatedAt）</param>
    void InitializeDefault(DateTime? currentTime = null);
}
