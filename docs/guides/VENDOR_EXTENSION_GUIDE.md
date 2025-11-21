# Vendor Driver Extension Guide / 厂商驱动扩展指南

## Quick Start / 快速开始

This guide shows how to add support for a new hardware vendor in the ZakYip Wheel Diverter Sorter system.

本指南说明如何为滚筒分拣机系统添加新硬件厂商的支持。

## Step 1: Create Vendor Directory / 创建厂商目录

Create a new directory under `Drivers/Vendors/`:

在 `Drivers/Vendors/` 下创建新目录：

```
Drivers/Vendors/
└── YourVendor/          # e.g., Omron, Mitsubishi, Rockwell
    ├── YourVendorVendorDriverFactory.cs
    ├── YourVendorDiverterController.cs
    ├── YourVendorIoLinkageDriver.cs
    └── ... (other vendor-specific drivers)
```

## Step 2: Implement Vendor Factory / 实现厂商工厂

Create a factory class that implements `IVendorDriverFactory`:

创建实现 `IVendorDriverFactory` 的工厂类：

```csharp
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.Hardware;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;

namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.YourVendor;

public class YourVendorVendorDriverFactory : IVendorDriverFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly YourVendorOptions _options;

    public VendorId VendorId => VendorId.YourVendor;

    public YourVendorVendorDriverFactory(
        ILoggerFactory loggerFactory,
        YourVendorOptions options)
    {
        _loggerFactory = loggerFactory;
        _options = options;
    }

    public VendorCapabilities GetCapabilities()
    {
        // Define your vendor's capabilities
        return new VendorCapabilities
        {
            VendorId = VendorId.YourVendor,
            VendorName = "Your Vendor Name",
            SupportsWheelDiverter = true,
            SupportsConveyorDrive = true,
            SupportsSensorInput = true,
            SupportsAlarmOutput = true,
            SupportsIoLinkage = true,
            SupportedProtocols = new[] { "TCP", "UDP", "Modbus" },
            MaxDiverterCount = 16,
            MaxConveyorSegmentCount = 8
        };
    }

    public IReadOnlyList<IWheelDiverterDriver> CreateWheelDiverterDrivers()
    {
        // Create and return your vendor's diverter drivers
        var drivers = new List<IWheelDiverterDriver>();
        
        // Implementation specific to your vendor
        // ...
        
        return drivers;
    }

    public IIoLinkageDriver CreateIoLinkageDriver()
    {
        // Create and return your vendor's IO linkage driver
        var logger = _loggerFactory.CreateLogger<YourVendorIoLinkageDriver>();
        return new YourVendorIoLinkageDriver(logger, /* vendor-specific params */);
    }

    public IConveyorSegmentDriver? CreateConveyorSegmentDriver(string segmentId)
    {
        // Create and return your vendor's conveyor driver
        // Return null if not supported
        return null;
    }
}
```

## Step 3: Update VendorId Enum / 更新 VendorId 枚举

Add your vendor to `Core/Hardware/VendorId.cs`:

在 `Core/Hardware/VendorId.cs` 中添加厂商：

```csharp
public enum VendorId
{
    // ... existing vendors
    
    /// <summary>
    /// Your Vendor Name
    /// </summary>
    [Description("Your Vendor Name")]
    YourVendor = 90
}
```

## Step 4: Add to VendorCapabilities / 添加到 VendorCapabilities

Add a static property in `Core/Hardware/VendorCapabilities.cs`:

在 `Core/Hardware/VendorCapabilities.cs` 中添加静态属性：

```csharp
public static VendorCapabilities YourVendor => new()
{
    VendorId = VendorId.YourVendor,
    VendorName = "Your Vendor Name",
    SupportsWheelDiverter = true,
    SupportsConveyorDrive = true,
    SupportsSensorInput = true,
    SupportsAlarmOutput = true,
    SupportsIoLinkage = true,
    SupportsDistributedLock = false,
    SupportedProtocols = new[] { "TCP", "UDP" },
    MaxDiverterCount = 0,
    MaxConveyorSegmentCount = 0
};
```

## Step 5: Register in DI / 在依赖注入中注册

Update `Drivers/DriverServiceExtensions.cs`:

更新 `Drivers/DriverServiceExtensions.cs`：

```csharp
return vendorId switch
{
    VendorId.Leadshine => new LeadshineVendorDriverFactory(loggerFactory, options.Leadshine),
    VendorId.Simulated => new SimulatedVendorDriverFactory(loggerFactory),
    VendorId.YourVendor => new YourVendorVendorDriverFactory(loggerFactory, options.YourVendor),  // Add this line
    _ => throw new NotSupportedException($"厂商 {vendorId} 尚未实现驱动工厂")
};
```

## Step 6: Add Configuration Options / 添加配置选项

Create options class in `Drivers/`:

在 `Drivers/` 中创建配置类：

```csharp
public class YourVendorOptions
{
    public string IpAddress { get; set; } = "192.168.1.100";
    public int Port { get; set; } = 502;
    // ... vendor-specific configuration
}
```

Update `DriverOptions.cs`:

更新 `DriverOptions.cs`：

```csharp
public class DriverOptions
{
    public bool UseHardwareDriver { get; set; } = false;
    public VendorId? VendorId { get; set; }
    
    public LeadshineOptions Leadshine { get; set; } = new();
    public YourVendorOptions YourVendor { get; set; } = new();  // Add this
}
```

## Step 7: Update Configuration File / 更新配置文件

Add configuration in `appsettings.json`:

在 `appsettings.json` 中添加配置：

```json
{
  "Driver": {
    "UseHardwareDriver": true,
    "VendorId": "YourVendor",
    "YourVendor": {
      "IpAddress": "192.168.1.100",
      "Port": 502
    }
  }
}
```

## Step 8: Implement Vendor-Specific Drivers / 实现厂商特定驱动

Implement the required driver interfaces:

实现必需的驱动接口：

1. **Diverter Controller** - Implements `IDiverterController`
2. **IO Linkage Driver** - Implements `IIoLinkageDriver`
3. **Conveyor Driver** (optional) - Implements `IConveyorSegmentDriver`

Example:

```csharp
namespace ZakYip.WheelDiverterSorter.Drivers.Vendors.YourVendor;

public class YourVendorDiverterController : IDiverterController
{
    public string DiverterId { get; }
    
    public async Task<bool> SetAngleAsync(int angle, CancellationToken cancellationToken = default)
    {
        // Vendor-specific implementation
        // Communicate with hardware via TCP/UDP/Modbus/etc.
        return true;
    }
    
    // ... implement other methods
}
```

## Testing / 测试

1. Create unit tests in `Drivers.Tests/YourVendor/`
2. Test factory creation
3. Test driver functionality
4. Test configuration switching

## Checklist / 检查清单

- [ ] Created vendor directory under `Drivers/Vendors/YourVendor/`
- [ ] Implemented `YourVendorVendorDriverFactory`
- [ ] Added `YourVendor` to `VendorId` enum
- [ ] Added static property in `VendorCapabilities`
- [ ] Registered factory in `DriverServiceExtensions`
- [ ] Created `YourVendorOptions` configuration class
- [ ] Updated `DriverOptions` with new vendor options
- [ ] Added configuration in `appsettings.json`
- [ ] Implemented vendor-specific drivers
- [ ] Created unit tests
- [ ] Verified build succeeds
- [ ] Tested vendor switching via configuration

## Example Vendors / 示例厂商

Reference existing implementations:

参考现有实现：

- **Leadshine** - `Drivers/Vendors/Leadshine/`
- **Siemens** - `Drivers/Vendors/Siemens/`
- **Simulated** - `Drivers/Vendors/Simulated/`

## Need Help? / 需要帮助？

Check the implementation summary: `PR24_IMPLEMENTATION_SUMMARY.md`
