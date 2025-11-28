using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;

/// <summary>
/// 通信配置仓储接口
/// </summary>
public interface ICommunicationConfigurationRepository
{
    /// <summary>
    /// 获取通信配置
    /// </summary>
    CommunicationConfiguration Get();

    /// <summary>
    /// 更新通信配置
    /// </summary>
    void Update(CommunicationConfiguration configuration);

    /// <summary>
    /// 初始化默认配置
    /// </summary>
    void InitializeDefault();
}
