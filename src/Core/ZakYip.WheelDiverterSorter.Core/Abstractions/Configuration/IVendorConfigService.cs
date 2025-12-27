using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

/// <summary>
/// 厂商配置服务接口（Core层 - 只读）
/// </summary>
/// <remarks>
/// <para><b>架构定位</b>：</para>
/// <list type="bullet">
///   <item>定义在 Core 层，供所有业务层（Execution、Application）使用</item>
///   <item>实现在 Application 层（通过 ISlidingConfigCache 提供缓存支持）</item>
///   <item>确保所有配置读取从内存缓存获取，避免高频 LiteDB 访问</item>
/// </list>
///
/// <para><b>缓存策略</b>：</para>
/// <list type="bullet">
///   <item>滑动过期时间：1 小时（无绝对过期）</item>
///   <item>热更新支持：配置更新后立即刷新缓存</item>
///   <item>单例模式：确保全局配置一致性</item>
/// </list>
/// </remarks>
public interface IVendorConfigService
{
    /// <summary>
    /// 获取IO驱动器配置（从缓存）
    /// </summary>
    /// <returns>IO驱动器配置</returns>
    DriverConfiguration GetDriverConfiguration();

    /// <summary>
    /// 获取感应IO配置（从缓存）
    /// </summary>
    /// <returns>感应IO配置</returns>
    SensorConfiguration GetSensorConfiguration();

    /// <summary>
    /// 获取摆轮配置（从缓存）
    /// </summary>
    /// <returns>摆轮配置</returns>
    WheelDiverterConfiguration GetWheelDiverterConfiguration();
}
