using ZakYip.WheelDiverterSorter.Core.IoBinding;

namespace ZakYip.WheelDiverterSorter.Core.Topology;

/// <summary>
/// 厂商IO地址 - 描述厂商特定的硬件地址
/// </summary>
/// <remarks>
/// 这是厂商驱动返回的具体硬件地址信息。
/// 不同厂商的地址格式可能不同（如卡号+点位号、PLC地址等）
/// </remarks>
public record class VendorIoAddress
{
    /// <summary>
    /// 逻辑名称（对应IoPointDescriptor的LogicalName）
    /// </summary>
    public required string LogicalName { get; init; }

    /// <summary>
    /// 厂商特定的地址字符串
    /// </summary>
    /// <remarks>
    /// 例如:
    /// - Leadshine: "Card0_Bit10"
    /// - Siemens: "I0.0", "Q1.5"
    /// - Modbus: "40001"
    /// </remarks>
    public required string VendorAddress { get; init; }

    /// <summary>
    /// 卡号/设备号（可选）
    /// </summary>
    public int? CardNumber { get; init; }

    /// <summary>
    /// 位号/点位号（可选）
    /// </summary>
    public int? BitNumber { get; init; }

    /// <summary>
    /// 附加参数（厂商特定）
    /// </summary>
    /// <remarks>
    /// 用于存储厂商特定的额外配置信息
    /// </remarks>
    public IReadOnlyDictionary<string, string>? AdditionalParameters { get; init; }
}

/// <summary>
/// 厂商IO映射器接口 - 将逻辑IO点映射到厂商特定地址
/// </summary>
/// <remarks>
/// 每个厂商实现此接口，提供从IoPointDescriptor到实际硬件地址的映射。
/// 这样新增厂商时，只需实现此接口即可。
/// </remarks>
public interface IVendorIoMapper
{
    /// <summary>
    /// 厂商标识
    /// </summary>
    string VendorId { get; }

    /// <summary>
    /// 将逻辑IO点映射到厂商地址
    /// </summary>
    /// <param name="ioPoint">逻辑IO点描述符</param>
    /// <returns>厂商特定的硬件地址，如果无法映射则返回null</returns>
    VendorIoAddress? MapIoPoint(IoPointDescriptor ioPoint);

    /// <summary>
    /// 批量映射IO点
    /// </summary>
    /// <param name="ioPoints">逻辑IO点列表</param>
    /// <returns>映射结果列表</returns>
    IReadOnlyList<VendorIoAddress> MapIoPoints(IEnumerable<IoPointDescriptor> ioPoints)
    {
        var results = new List<VendorIoAddress>();
        foreach (var ioPoint in ioPoints)
        {
            var address = MapIoPoint(ioPoint);
            if (address != null)
            {
                results.Add(address);
            }
        }
        return results;
    }

    /// <summary>
    /// 验证IO绑定配置是否与厂商兼容
    /// </summary>
    /// <param name="profile">IO绑定配置文件</param>
    /// <returns>验证结果（是否有效，错误信息）</returns>
    (bool IsValid, string? ErrorMessage) ValidateProfile(IoBindingProfile profile);
}
