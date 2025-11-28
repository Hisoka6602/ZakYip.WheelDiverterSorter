using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

/// <summary>
/// 传感器配置仓储接口
/// </summary>
public interface ISensorConfigurationRepository
{
    /// <summary>
    /// 获取传感器配置
    /// </summary>
    SensorConfiguration Get();

    /// <summary>
    /// 更新传感器配置
    /// </summary>
    void Update(SensorConfiguration configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    void InitializeDefault();
}
