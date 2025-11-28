using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.IoBinding;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology.Legacy;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated.IoMapping;

/// <summary>
/// 模拟驱动器IO映射器
/// </summary>
/// <remarks>
/// 模拟驱动器不需要实际的硬件映射，所有IO点都使用逻辑名称。
/// 主要用于开发和测试。
/// </remarks>
#pragma warning disable CS0618 // 遗留拓扑类型正在逐步迁移中
public class SimulatedIoMapper : IVendorIoMapper
{
    private readonly ILogger<SimulatedIoMapper> _logger;

    /// <inheritdoc/>
    public string VendorId => "Simulated";

    public SimulatedIoMapper(ILogger<SimulatedIoMapper> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public VendorIoAddress? MapIoPoint(IoPointDescriptor ioPoint)
    {
        // 模拟驱动器直接使用逻辑名称作为地址
        _logger.LogDebug("模拟映射IO点 {LogicalName}", ioPoint.LogicalName);

        return new VendorIoAddress
        {
            LogicalName = ioPoint.LogicalName,
            VendorAddress = $"Simulated_{ioPoint.LogicalName}"
        };
    }

    /// <inheritdoc/>
    public (bool IsValid, string? ErrorMessage) ValidateProfile(IoBindingProfile profile)
    {
        // 模拟驱动器接受所有IO点
        _logger.LogInformation("验证IO绑定配置文件 {ProfileId}，包含 {Count} 个IO点", 
            profile.ProfileId, profile.GetAllIoPoints().Count());

        return (true, null);
    }
}
