using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using CoreConfig = ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

namespace ZakYip.WheelDiverterSorter.Application.Services.Config;

/// <summary>
/// 厂商配置服务接口（Application层扩展）
/// </summary>
/// <remarks>
/// 提供厂商配置的统一访问门面，允许 Host 层通过此接口访问各厂商配置，
/// 而不需要直接引用 Drivers.Vendors.* 命名空间。
/// 继承 Core 层的 IVendorConfigService 接口，提供配置更新和高级操作。
/// </remarks>
public interface IVendorConfigService : CoreConfig.IVendorConfigService
{
    // Get methods inherited from CoreConfig.IVendorConfigService

    #region IO驱动器配置 (Leadshine/Siemens等)

    /// <summary>
    /// 更新IO驱动器配置
    /// </summary>
    /// <param name="config">新配置</param>
    void UpdateDriverConfiguration(DriverConfiguration config);

    /// <summary>
    /// 重置IO驱动器配置为默认值
    /// </summary>
    /// <returns>重置后的配置</returns>
    DriverConfiguration ResetDriverConfiguration();

    #endregion

    #region 感应IO配置

    /// <summary>
    /// 更新感应IO配置
    /// </summary>
    /// <param name="config">新配置</param>
    void UpdateSensorConfiguration(SensorConfiguration config);

    /// <summary>
    /// 重置感应IO配置为默认值
    /// </summary>
    /// <returns>重置后的配置</returns>
    SensorConfiguration ResetSensorConfiguration();

    #endregion

    #region 摆轮配置 (ShuDiNiao)

    /// <summary>
    /// 更新摆轮配置
    /// </summary>
    /// <param name="config">新配置</param>
    void UpdateWheelDiverterConfiguration(WheelDiverterConfiguration config);

    /// <summary>
    /// 获取数递鸟摆轮配置
    /// </summary>
    /// <returns>数递鸟配置，如果未配置则返回null</returns>
    ShuDiNiaoWheelDiverterConfig? GetShuDiNiaoConfiguration();

    /// <summary>
    /// 更新数递鸟摆轮配置
    /// </summary>
    /// <param name="shuDiNiaoConfig">数递鸟配置</param>
    void UpdateShuDiNiaoConfiguration(ShuDiNiaoWheelDiverterConfig shuDiNiaoConfig);

    #endregion
}
