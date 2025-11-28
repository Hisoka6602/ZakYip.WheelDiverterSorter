using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.IoBinding;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology.Legacy;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine.IoMapping;

/// <summary>
/// 雷赛控制器IO映射器
/// </summary>
/// <remarks>
/// 将逻辑IO点映射到雷赛控制器的实际点位。
/// 雷赛使用卡号+位号的方式定义IO点。
/// </remarks>
#pragma warning disable CS0618 // 遗留拓扑类型正在逐步迁移中
public class LeadshineIoMapper : IVendorIoMapper
{
    private readonly ILogger<LeadshineIoMapper> _logger;
    private readonly LeadshineIoMappingConfig _config;

    /// <inheritdoc/>
    public string VendorId => "Leadshine";

    public LeadshineIoMapper(
        ILogger<LeadshineIoMapper> logger,
        LeadshineIoMappingConfig config)
    {
        _logger = logger;
        _config = config;
    }

    /// <inheritdoc/>
    public VendorIoAddress? MapIoPoint(IoPointDescriptor ioPoint)
    {
        // 尝试从配置中查找映射
        if (_config.PointMappings.TryGetValue(ioPoint.LogicalName, out var mapping))
        {
            _logger.LogDebug("映射IO点 {LogicalName} -> Card={CardNo}, Bit={BitNo}", 
                ioPoint.LogicalName, mapping.CardNumber, mapping.BitNumber);

            return new VendorIoAddress
            {
                LogicalName = ioPoint.LogicalName,
                VendorAddress = $"Card{mapping.CardNumber}_Bit{mapping.BitNumber}",
                CardNumber = mapping.CardNumber,
                BitNumber = mapping.BitNumber
            };
        }

        // 如果未找到映射，尝试使用约定规则
        var conventionMapping = TryMapByConvention(ioPoint);
        if (conventionMapping != null)
        {
            _logger.LogDebug("通过约定映射IO点 {LogicalName} -> {VendorAddress}", 
                ioPoint.LogicalName, conventionMapping.VendorAddress);
            return conventionMapping;
        }

        _logger.LogWarning("无法映射IO点 {LogicalName}，未找到配置或约定规则", ioPoint.LogicalName);
        return null;
    }

    /// <inheritdoc/>
    public (bool IsValid, string? ErrorMessage) ValidateProfile(IoBindingProfile profile)
    {
        // 检查所有IO点是否都能映射
        var unmappedPoints = new List<string>();
        
        foreach (var ioPoint in profile.GetAllIoPoints())
        {
            if (MapIoPoint(ioPoint) == null)
            {
                unmappedPoints.Add(ioPoint.LogicalName);
            }
        }

        if (unmappedPoints.Any())
        {
            return (false, $"以下IO点无法映射到雷赛控制器: {string.Join(", ", unmappedPoints)}");
        }

        return (true, null);
    }

    /// <summary>
    /// 尝试通过约定规则映射IO点
    /// </summary>
    /// <remarks>
    /// 约定规则示例：
    /// - 入口传感器: Card0_Bit0
    /// - 节点传感器 D1: Card0_Bit10
    /// - 节点左转执行器 D1: Card0_Bit0-1
    /// - 节点右转执行器 D1: Card0_Bit2-3
    /// </remarks>
    private VendorIoAddress? TryMapByConvention(IoPointDescriptor ioPoint)
    {
        // 这里可以实现基于命名约定的自动映射逻辑
        // 例如: "D1_Left" -> Card0_Bit0
        //       "D2_Left" -> Card0_Bit2
        //       "D1_Sensor" -> Card0_Bit10
        
        // 简单实现：如果未配置，返回null，要求显式配置
        return null;
    }
}

/// <summary>
/// 雷赛IO映射配置
/// </summary>
public class LeadshineIoMappingConfig
{
    private ImmutableDictionary<string, LeadshinePointMapping> _pointMappings = ImmutableDictionary<string, LeadshinePointMapping>.Empty;

    /// <summary>
    /// IO点映射表
    /// Key: 逻辑名称, Value: 雷赛点位映射
    /// </summary>
    /// <remarks>
    /// 使用 ImmutableDictionary 确保线程安全。
    /// 配置在初始化时设置，之后只读访问。
    /// </remarks>
    public ImmutableDictionary<string, LeadshinePointMapping> PointMappings 
    { 
        get => _pointMappings;
        set => _pointMappings = value;
    }

    /// <summary>
    /// 默认卡号
    /// </summary>
    public int DefaultCardNumber { get; set; } = 0;
}

/// <summary>
/// 雷赛点位映射
/// </summary>
public class LeadshinePointMapping
{
    /// <summary>
    /// 卡号
    /// </summary>
    public int CardNumber { get; set; }

    /// <summary>
    /// 位号
    /// </summary>
    public int BitNumber { get; set; }
}
