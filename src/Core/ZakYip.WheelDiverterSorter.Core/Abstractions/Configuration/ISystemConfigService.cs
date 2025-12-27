using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

/// <summary>
/// 系统配置服务接口（提供缓存配置访问）
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
///
/// <para><b>使用场景</b>：</para>
/// <list type="bullet">
///   <item>分拣逻辑（Execution 层）：获取异常格口ID、分拣模式等配置</item>
///   <item>API 控制器（Host 层）：读取和更新系统配置</item>
///   <item>后台服务（Host 层）：定期检查配置一致性</item>
/// </list>
///
/// <para><b>禁止使用</b>：</para>
/// <list type="bullet">
///   <item>不要在分拣热路径直接使用 ISystemConfigurationRepository.Get()</item>
///   <item>所有配置读取应通过本接口的 GetSystemConfig() 方法</item>
/// </list>
/// </remarks>
public interface ISystemConfigService
{
    /// <summary>
    /// 获取当前系统配置（从缓存）
    /// </summary>
    /// <returns>系统配置</returns>
    /// <remarks>
    /// 此方法优先从内存缓存获取配置，避免直接访问 LiteDB。
    /// 首次调用时会访问数据库并缓存结果，后续调用直接返回缓存值。
    /// 缓存策略：1小时滑动过期，配置更新后立即刷新。
    /// </remarks>
    SystemConfiguration GetSystemConfig();
}
