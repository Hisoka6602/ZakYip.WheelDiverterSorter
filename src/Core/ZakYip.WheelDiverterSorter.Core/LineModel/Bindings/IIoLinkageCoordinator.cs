using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;

/// <summary>
/// IO 联动协调器接口。
/// 负责根据系统运行状态，协调中段皮带等设备的 IO 联动控制。
/// </summary>
public interface IIoLinkageCoordinator
{
    /// <summary>
    /// 根据系统当前状态，确定需要设置的 IO 联动点列表。
    /// </summary>
    /// <param name="systemState">系统运行状态</param>
    /// <param name="options">IO 联动配置选项</param>
    /// <returns>需要设置的 IO 联动点列表</returns>
    IReadOnlyList<IoLinkagePoint> DetermineIoLinkagePoints(
        SystemOperatingState systemState,
        IoLinkageOptions options);

    /// <summary>
    /// 判断 IO 联动功能是否应当激活。
    /// </summary>
    /// <param name="systemState">系统运行状态</param>
    /// <returns>如果应当激活返回 true，否则返回 false</returns>
    bool ShouldActivateIoLinkage(SystemOperatingState systemState);
}
