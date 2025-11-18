namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 线体拓扑配置提供者接口
/// </summary>
/// <remarks>
/// 定义统一的拓扑配置获取接口，支持从不同来源（JSON、数据库、API等）加载配置
/// </remarks>
public interface ILineTopologyConfigProvider
{
    /// <summary>
    /// 获取线体拓扑配置
    /// </summary>
    /// <returns>线体拓扑配置对象</returns>
    Task<LineTopologyConfig> GetTopologyAsync();

    /// <summary>
    /// 刷新配置（重新加载）
    /// </summary>
    Task RefreshAsync();
}
