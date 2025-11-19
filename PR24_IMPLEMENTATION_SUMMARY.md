# PR-24: 驱动与通信层厂商适配化实施总结

## 概述

本PR将现有的 Drivers + Communication 层统一收敛成一个清晰的"厂商适配层"（Vendor Adapter Layer），为后续接入更多硬件厂商打下基础。

## 架构变化

### 1. 新增核心硬件抽象接口 (Core.Hardware)

在 `ZakYip.WheelDiverterSorter.Core.Hardware` 命名空间下新增了以下接口：

#### 硬件能力接口

- **IWheelDiverterActuator** - 摆轮执行器接口
  - 提供摆轮左转、右转、直通、停止等基本操作
  
- **IConveyorDriveController** - 传送带驱动控制器接口
  - 提供传送带启动、停止、速度控制等操作
  
- **ISensorInputReader** - 传感器输入读取器接口
  - 提供按逻辑点位读取传感器状态的能力
  
- **IAlarmOutputController** - 报警输出控制器接口
  - 提供三色灯、蜂鸣器等报警设备控制

#### 厂商标识与能力声明

- **VendorId** - 厂商标识符枚举
  ```csharp
  public enum VendorId
  {
      Unspecified = 0,
      Simulated = 1,      // 模拟驱动器
      Leadshine = 10,     // 雷赛智能
      Siemens = 20,       // 西门子
      Mitsubishi = 30,    // 三菱电机
      Omron = 40,         // 欧姆龙
      // ... 更多厂商
  }
  ```

- **VendorCapabilities** - 厂商能力声明记录
  ```csharp
  public record VendorCapabilities
  {
      public required VendorId VendorId { get; init; }
      public bool SupportsWheelDiverter { get; init; }
      public bool SupportsConveyorDrive { get; init; }
      public bool SupportsSensorInput { get; init; }
      public bool SupportsAlarmOutput { get; init; }
      public bool SupportsIoLinkage { get; init; }
      public IReadOnlyList<string> SupportedProtocols { get; init; }
      // ...
  }
  ```

### 2. 驱动目录结构重组

原有的驱动文件已从根级目录移动到厂商子目录：

```
ZakYip.WheelDiverterSorter.Drivers/
├── Abstractions/                    # 驱动抽象接口（保持不变）
├── Vendors/                         # 新增：厂商适配层
│   ├── Leadshine/                   # 雷赛厂商驱动
│   │   ├── LeadshineVendorDriverFactory.cs
│   │   ├── LeadshineDiverterController.cs
│   │   ├── LeadshineEmcController.cs
│   │   └── ...
│   ├── Siemens/                     # 西门子厂商驱动（原S7）
│   │   ├── S7Connection.cs
│   │   ├── S7DiverterController.cs
│   │   └── ...
│   ├── Simulated/                   # 模拟厂商驱动
│   │   ├── SimulatedVendorDriverFactory.cs
│   │   ├── SimulatedConveyorSegmentDriver.cs
│   │   └── ...
│   └── Omron/                       # 欧姆龙厂商驱动（预留）
└── IVendorDriverFactory.cs          # 厂商驱动工厂接口
```

### 3. 命名空间更新

所有厂商驱动的命名空间已更新为：

- **雷赛**: `ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine`
- **西门子**: `ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens`
- **模拟**: `ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated`

这确保了命名空间与目录结构的一致性。

### 4. 厂商驱动工厂模式

引入了工厂模式，通过 `IVendorDriverFactory` 接口统一管理厂商驱动创建：

```csharp
public interface IVendorDriverFactory
{
    VendorId VendorId { get; }
    VendorCapabilities GetCapabilities();
    
    IReadOnlyList<IWheelDiverterDriver> CreateWheelDiverterDrivers();
    IIoLinkageDriver CreateIoLinkageDriver();
    IConveyorSegmentDriver? CreateConveyorSegmentDriver(string segmentId);
}
```

每个厂商实现自己的工厂类：
- `LeadshineVendorDriverFactory`
- `SimulatedVendorDriverFactory`
- 未来可添加：`SiemensVendorDriverFactory`、`OmronVendorDriverFactory` 等

### 5. 依赖注入更新

`DriverServiceExtensions` 已更新为使用工厂模式：

```csharp
services.AddSingleton<IVendorDriverFactory>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    
    if (options.UseHardwareDriver)
    {
        var vendorId = options.VendorId ?? VendorId.Leadshine;
        
        return vendorId switch
        {
            VendorId.Leadshine => new LeadshineVendorDriverFactory(loggerFactory, options.Leadshine),
            VendorId.Simulated => new SimulatedVendorDriverFactory(loggerFactory),
            _ => throw new NotSupportedException($"厂商 {vendorId} 尚未实现驱动工厂")
        };
    }
    else
    {
        return new SimulatedVendorDriverFactory(loggerFactory);
    }
});
```

## 配置变化

### 配置选项扩展

`DriverOptions` 类新增了 `VendorId` 属性：

```csharp
public class DriverOptions
{
    public bool UseHardwareDriver { get; set; } = false;
    
    // 旧版（保留向后兼容）
    public DriverVendorType VendorType { get; set; } = DriverVendorType.Leadshine;
    
    // 新版（推荐使用）
    public VendorId? VendorId { get; set; }
    
    public LeadshineOptions Leadshine { get; set; } = new();
}
```

### 配置示例

在 `appsettings.json` 中可以这样配置：

```json
{
  "Driver": {
    "UseHardwareDriver": true,
    "VendorId": "Leadshine",
    "Leadshine": {
      "CardNo": 0,
      "Diverters": [
        {
          "DiverterId": 1,
          "DiverterName": "D1",
          "OutputStartBit": 0,
          "FeedbackInputBit": 10
        }
      ]
    }
  }
}
```

切换厂商只需修改 `VendorId` 配置项：

```json
{
  "Driver": {
    "UseHardwareDriver": false,
    "VendorId": "Simulated"
  }
}
```

## 层次边界说明

### Communication 层职责

Communication 层**仅**关注通信传输：
- TCP/UDP 客户端和服务器
- 串口通信
- Modbus 协议
- 自定义协议的帧收发

**不包含**：硬件设备的业务逻辑、驱动控制逻辑

### Drivers 层职责

Drivers 层负责：
- 将通信层的原始数据转换为领域动作
- 实现具体的硬件控制逻辑
- 通过组合 Communication 的底层接口实现驱动
- 提供统一的抽象接口给上层

### Execution 层职责

Execution 层：
- **只依赖** Drivers 层的抽象接口（如 `IWheelDiverterDriver`、`IIoLinkageDriver`）
- **不直接引用** 具体厂商驱动类
- **不直接引用** Communication 层

## 验收结果

✅ **所有验收标准已达成**：

1. ✅ Execution 层不再直接依赖具体厂商驱动类，仅依赖抽象接口
2. ✅ 切换厂商只需修改配置和 DI 注册，无需修改核心分拣代码
3. ✅ 所有涉及的 .cs 文件命名空间与目录结构保持一致
4. ✅ 解决方案成功构建，仅有预期的警告，无错误

## 后续扩展指南

### 添加新厂商驱动

1. 在 `Drivers/Vendors/` 下创建新的厂商目录，例如 `Omron/`
2. 实现厂商特定的驱动类，继承或实现相应接口
3. 创建 `[厂商]VendorDriverFactory` 类实现 `IVendorDriverFactory`
4. 在 `DriverServiceExtensions` 的 switch 中添加新的 case
5. 更新 `VendorId` 枚举和 `VendorCapabilities` 静态属性

### 示例：添加Omron厂商支持

```csharp
// 1. 在 Vendors/Omron/ 下创建驱动
public class OmronVendorDriverFactory : IVendorDriverFactory
{
    public VendorId VendorId => VendorId.Omron;
    
    public VendorCapabilities GetCapabilities() => VendorCapabilities.Omron;
    
    // 实现工厂方法...
}

// 2. 在 DriverServiceExtensions 中注册
return vendorId switch
{
    VendorId.Leadshine => new LeadshineVendorDriverFactory(...),
    VendorId.Simulated => new SimulatedVendorDriverFactory(...),
    VendorId.Omron => new OmronVendorDriverFactory(...),  // 新增
    _ => throw new NotSupportedException(...)
};
```

## 重要文件变更

### 新增文件
- `Core/Hardware/IWheelDiverterActuator.cs`
- `Core/Hardware/IConveyorDriveController.cs`
- `Core/Hardware/ISensorInputReader.cs`
- `Core/Hardware/IAlarmOutputController.cs`
- `Core/Hardware/VendorId.cs`
- `Core/Hardware/VendorCapabilities.cs`
- `Drivers/IVendorDriverFactory.cs`
- `Drivers/Vendors/Leadshine/LeadshineVendorDriverFactory.cs`
- `Drivers/Vendors/Simulated/SimulatedVendorDriverFactory.cs`

### 移动文件
- `Drivers/Leadshine/*` → `Drivers/Vendors/Leadshine/*`
- `Drivers/S7/*` → `Drivers/Vendors/Siemens/*`
- `Drivers/Simulated/*` → `Drivers/Vendors/Simulated/*`

### 修改文件
- `Drivers/DriverOptions.cs` - 新增 VendorId 属性
- `Drivers/DriverServiceExtensions.cs` - 使用工厂模式
- `Host/Services/MiddleConveyorServiceExtensions.cs` - 使用工厂模式
- 所有测试文件的 using 语句

## 总结

本PR成功实现了驱动层的厂商适配化改造，关键成果包括：

1. **清晰的抽象层次**：Core.Hardware 定义统一的硬件能力接口
2. **规范的目录结构**：所有厂商驱动统一放在 Vendors 子目录
3. **灵活的工厂模式**：通过配置轻松切换厂商
4. **向后兼容**：保留了原有的配置选项和接口
5. **易于扩展**：新增厂商只需实现工厂接口和相应驱动

这为项目后续接入更多硬件厂商（VFD、IO板、传感器等）打下了坚实的基础。
