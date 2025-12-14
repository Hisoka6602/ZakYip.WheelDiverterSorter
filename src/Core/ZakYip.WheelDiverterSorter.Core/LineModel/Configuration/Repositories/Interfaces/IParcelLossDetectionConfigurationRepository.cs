using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

/// <summary>
/// 包裹丢失检测配置仓储接口
/// </summary>
public interface IParcelLossDetectionConfigurationRepository
{
    /// <summary>
    /// 获取包裹丢失检测配置
    /// </summary>
    ParcelLossDetectionConfiguration Get();

    /// <summary>
    /// 更新包裹丢失检测配置
    /// </summary>
    void Update(ParcelLossDetectionConfiguration configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    void InitializeDefault();
}
