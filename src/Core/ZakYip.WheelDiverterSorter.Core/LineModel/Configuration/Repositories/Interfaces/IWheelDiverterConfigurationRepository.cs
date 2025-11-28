using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

/// <summary>
/// 摆轮配置仓储接口
/// </summary>
public interface IWheelDiverterConfigurationRepository
{
    /// <summary>
    /// 获取摆轮配置
    /// </summary>
    WheelDiverterConfiguration Get();

    /// <summary>
    /// 更新摆轮配置
    /// </summary>
    void Update(WheelDiverterConfiguration configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    void InitializeDefault();
}
