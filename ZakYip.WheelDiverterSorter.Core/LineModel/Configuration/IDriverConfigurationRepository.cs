namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 驱动器配置仓储接口
/// </summary>
public interface IDriverConfigurationRepository
{
    /// <summary>
    /// 获取驱动器配置
    /// </summary>
    DriverConfiguration Get();

    /// <summary>
    /// 更新驱动器配置
    /// </summary>
    void Update(DriverConfiguration configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    void InitializeDefault();
}
