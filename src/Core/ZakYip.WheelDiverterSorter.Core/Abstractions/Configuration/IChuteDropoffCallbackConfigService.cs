using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

/// <summary>
/// 格口落格回调配置服务接口（Core层 - 只读）
/// </summary>
/// <remarks>
/// <para><b>架构定位</b>：</para>
/// <list type="bullet">
///   <item>定义在 Core 层，供所有业务层（Execution、Application）使用</item>
///   <item>确保所有配置读取从内存缓存获取，避免高频 LiteDB 访问</item>
/// </list>
/// </remarks>
public interface IChuteDropoffCallbackConfigService
{
    /// <summary>
    /// 获取格口落格回调配置（从缓存）
    /// </summary>
    /// <returns>落格回调配置</returns>
    ChuteDropoffCallbackConfiguration GetCallbackConfiguration();
}
