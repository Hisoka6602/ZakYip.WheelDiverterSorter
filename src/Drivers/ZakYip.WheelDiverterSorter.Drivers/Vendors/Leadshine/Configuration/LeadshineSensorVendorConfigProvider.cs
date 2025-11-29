using ZakYip.WheelDiverterSorter.Core.Abstractions.Drivers;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine.Configuration;

/// <summary>
/// 雷赛传感器配置提供者
/// </summary>
/// <remarks>
/// 实现 ISensorVendorConfigProvider 接口，提供雷赛传感器的厂商无关配置访问。
/// </remarks>
public sealed class LeadshineSensorVendorConfigProvider : ISensorVendorConfigProvider
{
    private readonly LeadshineSensorOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">雷赛传感器配置</param>
    public LeadshineSensorVendorConfigProvider(LeadshineSensorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public bool UseHardwareSensor => true;

    /// <inheritdoc/>
    public string VendorTypeName => "Leadshine";

    /// <inheritdoc/>
    public ushort CardNo => _options.CardNo;

    /// <inheritdoc/>
    public IReadOnlyList<SensorConfigEntry> GetSensorConfigs()
    {
        return _options.Sensors
            .Select(s => new SensorConfigEntry
            {
                SensorId = s.SensorId,
                SensorTypeName = s.Type.ToString(),
                InputBit = s.InputBit,
                IsEnabled = s.IsEnabled
            })
            .ToList()
            .AsReadOnly();
    }
}
