using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

/// <summary>
/// 路由配置仓储接口，负责管理格口到摆轮的映射关系
/// </summary>
public interface IRouteConfigurationRepository
{
    /// <summary>
    /// 根据格口ID获取路由配置
    /// </summary>
    /// <param name="chuteId">格口标识（数字ID）</param>
    /// <returns>格口路由配置，如果不存在则返回null</returns>
    ChuteRouteConfiguration? GetByChuteId(long chuteId);

    /// <summary>
    /// 获取所有启用的路由配置
    /// </summary>
    /// <returns>所有启用的格口路由配置列表</returns>
    IEnumerable<ChuteRouteConfiguration> GetAllEnabled();

    /// <summary>
    /// 添加或更新路由配置
    /// </summary>
    /// <param name="configuration">路由配置</param>
    void Upsert(ChuteRouteConfiguration configuration);

    /// <summary>
    /// 删除指定格口的路由配置
    /// </summary>
    /// <param name="chuteId">格口标识（数字ID）</param>
    /// <returns>是否删除成功</returns>
    bool Delete(long chuteId);

    /// <summary>
    /// 初始化默认配置数据
    /// </summary>
    void InitializeDefaultData();
}
