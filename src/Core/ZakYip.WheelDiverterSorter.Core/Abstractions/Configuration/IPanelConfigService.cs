using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

/// <summary>
/// 面板配置服务接口
/// </summary>
/// <remarks>
/// 支持配置缓存与热更新：
/// - 读取：通过统一滑动缓存（1小时过期）
/// - 更新：先写 LiteDB，再立即刷新缓存
/// </remarks>
public interface IPanelConfigService
{
    /// <summary>
    /// 获取面板配置（从缓存）
    /// </summary>
    /// <returns>面板配置</returns>
    PanelConfiguration GetPanelConfig();

    /// <summary>
    /// 更新面板配置并刷新缓存
    /// </summary>
    /// <param name="configuration">面板配置</param>
    void UpdatePanelConfig(PanelConfiguration configuration);

    /// <summary>
    /// 刷新面板配置缓存（从数据库重新加载）
    /// </summary>
    /// <returns>刷新后的面板配置</returns>
    /// <remarks>
    /// 用于配置热更新场景：当外部直接更新数据库后，
    /// 调用此方法可立即刷新缓存，确保后续 GetPanelConfig() 返回最新值。
    /// </remarks>
    PanelConfiguration RefreshCacheFromRepository();
}
