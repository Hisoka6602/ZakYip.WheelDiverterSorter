using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

/// <summary>
/// 传感器配置服务接口
/// </summary>
/// <remarks>
/// 支持配置缓存与热更新：
/// - 读取：通过统一滑动缓存（1小时过期）
/// - 更新：先写 LiteDB，再立即刷新缓存
/// </remarks>
public interface ISensorConfigService
{
    /// <summary>
    /// 获取传感器配置（从缓存）
    /// </summary>
    /// <returns>传感器配置</returns>
    SensorConfiguration GetSensorConfig();

    /// <summary>
    /// 更新传感器配置并刷新缓存
    /// </summary>
    /// <param name="configuration">传感器配置</param>
    void UpdateSensorConfig(SensorConfiguration configuration);

    /// <summary>
    /// 刷新传感器配置缓存（从数据库重新加载）
    /// </summary>
    /// <returns>刷新后的传感器配置</returns>
    /// <remarks>
    /// 用于配置热更新场景：当外部直接更新数据库后，
    /// 调用此方法可立即刷新缓存，确保后续 GetSensorConfig() 返回最新值。
    /// </remarks>
    SensorConfiguration RefreshCacheFromRepository();
}
