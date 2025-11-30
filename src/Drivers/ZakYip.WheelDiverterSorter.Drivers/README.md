# ZakYip.WheelDiverterSorter.Drivers

本项目包含用于控制摆轮分拣设备的硬件驱动程序和接口。

## 项目参考

本项目参考了 [ZakYip.Singulation](https://github.com/Hisoka6602/ZakYip.Singulation) 的IO厂商和读写逻辑实现。

## 功能特性

- ✅ **抽象接口**: 定义了IO端口和摆轮控制器的标准接口（定义在 Core 层）
- ✅ **多厂商支持**: 支持雷赛（Leadshine）、西门子（Siemens S7）、摩迪（Modi）、书迪鸟（ShuDiNiao）等厂商设备
- ✅ **硬件执行器**: 提供真实硬件的路径执行器实现
- ✅ **仿真驱动**: 支持 Simulated 驱动用于开发测试
- ✅ **配置化**: 支持通过配置文件管理驱动器和摆轮配置
- ✅ **可切换**: 支持在模拟驱动器和硬件驱动器之间切换

## 项目结构

```
ZakYip.WheelDiverterSorter.Drivers/
├── Diagnostics/                     # 驱动诊断
│   └── RelayWheelDiverterSelfTest.cs
├── Vendors/                         # 厂商特定实现
│   ├── Leadshine/                   # 雷赛 IO 卡驱动
│   │   ├── LTDMC.cs                 # 雷赛 SDK P/Invoke 封装
│   │   ├── LeadshineInputPort.cs
│   │   ├── LeadshineOutputPort.cs
│   │   ├── LeadshineWheelDiverterDriver.cs  # 实现 IWheelDiverterDriver
│   │   ├── LeadshineConveyorSegmentDriver.cs
│   │   ├── LeadshineIoLinkageDriver.cs
│   │   ├── LeadshineEmcController.cs
│   │   ├── CoordinatedEmcController.cs
│   │   ├── LeadshineVendorDriverFactory.cs
│   │   └── IoMapping/
│   │       └── LeadshineIoMapper.cs
│   ├── Siemens/                     # 西门子 S7 PLC 驱动
│   │   ├── S7Connection.cs
│   │   ├── S7WheelDiverterDriver.cs  # 实现 IWheelDiverterDriver
│   │   ├── S7InputPort.cs
│   │   └── S7OutputPort.cs
│   ├── Modi/                        # 摩迪摆轮协议驱动
│   │   ├── ModiProtocol.cs
│   │   ├── ModiWheelDiverterDriver.cs  # 实现 IWheelDiverterDriver
│   │   └── ModiSimulatedDevice.cs
│   ├── ShuDiNiao/                   # 书迪鸟摆轮协议驱动
│   │   ├── ShuDiNiaoProtocol.cs
│   │   ├── ShuDiNiaoWheelDiverterDriver.cs  # 实现 IWheelDiverterDriver
│   │   ├── ShuDiNiaoWheelDiverterDriverManager.cs
│   │   ├── ShuDiNiaoWheelDiverterDeviceAdapter.cs  # IWheelDiverterDriver -> IWheelDiverterDevice 适配器
│   │   └── ShuDiNiaoSimulatedDevice.cs
│   └── Simulated/                   # 仿真驱动实现
│       ├── SimulatedWheelDiverterDevice.cs  # 实现 IWheelDiverterDevice
│       ├── SimulatedConveyorSegmentDriver.cs
│       ├── SimulatedIoLinkageDriver.cs
│       ├── SimulatedVendorDriverFactory.cs
│       └── IoMapping/
├── FactoryBasedDriverManager.cs     # 工厂模式驱动管理器
├── HardwareSwitchingPathExecutor.cs # 硬件路径执行器
├── WheelCommandExecutor.cs          # 摆轮命令执行器
├── IoLinkageExecutor.cs             # IO 联动执行器
├── DriverServiceExtensions.cs       # DI 扩展方法
└── DriverOptions.cs                 # 驱动配置选项
```

**注意**: HAL（硬件抽象层）接口统一定义在 `Core/Hardware/` 目录下：
- `IWheelDiverterDevice` - 基于命令的摆轮设备接口（高层 HAL）
- `IWheelDiverterDriver` - 基于方向的摆轮驱动接口（低层 HAL，位于 `Core/Hardware/Devices/`）
- `IInputPort`, `IOutputPort` - IO 端口接口（位于 `Core/Hardware/Ports/`）

本项目提供具体厂商实现。所有摆轮实现统一命名为 `<VendorName>WheelDiverterDriver` 或 `<VendorName>WheelDiverterDevice`。

## 快速开始

### 1. 配置驱动器

在 `appsettings.json` 中添加驱动器配置：

```json
{
  "Driver": {
    "UseHardwareDriver": false,  // true=使用硬件驱动，false=使用模拟驱动
    "Leadshine": {
      "CardNo": 0,  // 雷赛控制器卡号
      "Diverters": [
        {
          "DiverterId": "D1",
          "OutputStartBit": 0,
          "FeedbackInputBit": 10
        },
        {
          "DiverterId": "D2",
          "OutputStartBit": 2,
          "FeedbackInputBit": 11
        },
        {
          "DiverterId": "D3",
          "OutputStartBit": 4,
          "FeedbackInputBit": 12
        }
      ]
    }
  }
}
```

### 2. 注册服务

在 `Program.cs` 中注册驱动器服务：

```csharp
using ZakYip.WheelDiverterSorter.Drivers;

var builder = WebApplication.CreateBuilder(args);

// 添加驱动器服务
builder.Services.AddDriverServices(builder.Configuration);

var app = builder.Build();
app.Run();
```

### 3. 使用执行器

执行器会根据配置自动选择模拟或硬件实现：

```csharp
public class SortingService
{
    private readonly ISwitchingPathExecutor _executor;

    public SortingService(ISwitchingPathExecutor executor)
    {
        _executor = executor;
    }

    public async Task<PathExecutionResult> SortAsync(SwitchingPath path)
    {
        // 执行器会根据配置使用硬件或模拟实现
        return await _executor.ExecuteAsync(path);
    }
}
```

## 接口说明

### IInputPort - 输入端口接口

用于从硬件设备读取输入信号：

```csharp
public interface IInputPort
{
    Task<bool> ReadAsync(int bitIndex);
    Task<bool[]> ReadBatchAsync(int startBit, int count);
}
```

### IOutputPort - 输出端口接口

用于向硬件设备写入控制信号：

```csharp
public interface IOutputPort
{
    Task<bool> WriteAsync(int bitIndex, bool value);
    Task<bool> WriteBatchAsync(int startBit, bool[] values);
}
```

### IWheelDiverterDriver - 摆轮驱动接口

统一的摆轮驱动接口（HAL - 硬件抽象层），基于方向控制：

```csharp
public interface IWheelDiverterDriver
{
    string DiverterId { get; }
    Task<bool> TurnLeftAsync(CancellationToken cancellationToken = default);
    Task<bool> TurnRightAsync(CancellationToken cancellationToken = default);
    Task<bool> PassThroughAsync(CancellationToken cancellationToken = default);
    Task<bool> StopAsync(CancellationToken cancellationToken = default);
    Task<string> GetStatusAsync();
}
```

### IWheelDiverterDevice - 摆轮设备接口

基于命令的高层摆轮设备接口（HAL），用于执行层集成：

```csharp
public interface IWheelDiverterDevice
{
    string DeviceId { get; }
    Task<OperationResult> ExecuteAsync(WheelCommand command, CancellationToken cancellationToken = default);
    Task<OperationResult> StopAsync(CancellationToken cancellationToken = default);
    Task<WheelDiverterState> GetStateAsync(CancellationToken cancellationToken = default);
}
```

## 硬件支持

### 支持的厂商

| 厂商 | 目录 | 说明 |
|------|------|------|
| 雷赛 (Leadshine) | `Vendors/Leadshine/` | LTDMC 系列运动控制卡 |
| 西门子 (Siemens) | `Vendors/Siemens/` | S7 系列 PLC |
| 摩迪 (Modi) | `Vendors/Modi/` | 摩迪摆轮协议 |
| 书迪鸟 (ShuDiNiao) | `Vendors/ShuDiNiao/` | 书迪鸟摆轮协议 |
| 仿真 (Simulated) | `Vendors/Simulated/` | 开发测试用模拟驱动 |

### 雷赛（Leadshine）运动控制器

本项目支持雷赛LTDMC系列运动控制卡，用于控制摆轮角度。

**支持的角度**: 0°, 30°, 45°, 90°

**角度编码方式**: 使用2个输出位进行二进制编码

| 角度 | Bit1 | Bit0 |
|------|------|------|
| 0°   | 0    | 0    |
| 30°  | 0    | 1    |
| 45°  | 1    | 0    |
| 90°  | 1    | 1    |

详细说明请参考: [Vendors/Leadshine/](Vendors/Leadshine/)

### 扩展其他厂商

要添加其他厂商的支持，请在 `Vendors/` 目录下创建新的厂商目录，并：

1. 实现 Core 层定义的 HAL 接口（`IWheelDiverterDriver` 或 `IWheelDiverterDevice`）
2. 创建对应的配置类和工厂类（实现 `IVendorDriverFactory`）
3. 在 `DriverServiceExtensions` 中添加注册逻辑

**命名规范**：所有摆轮实现必须命名为 `<VendorName>WheelDiverterDriver` 或 `<VendorName>WheelDiverterDevice`。禁止使用 `*DiverterController` 命名。

**注意**：所有厂商实现必须放在 `Vendors/<VendorName>/` 目录下，不允许在其他位置创建厂商实现。

## 配置选项

### DriverOptions

| 属性 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| UseHardwareDriver | bool | 是否使用硬件驱动器 | false |
| Leadshine | LeadshineOptions | 雷赛控制器配置 | - |

### LeadshineOptions

| 属性 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| CardNo | ushort | 控制器卡号 | 0 |
| Diverters | List | 摆轮配置列表 | [] |

### LeadshineDiverterConfigDto

| 属性 | 类型 | 说明 | 必填 |
|------|------|------|------|
| DiverterId | string | 摆轮ID | 是 |
| OutputStartBit | int | 输出起始位 | 是 |
| FeedbackInputBit | int? | 反馈输入位 | 否 |

## 开发与测试

### 开发模式

在开发阶段，建议使用模拟驱动器：

```json
{
  "Driver": {
    "UseHardwareDriver": false
  }
}
```

### 生产模式

在生产环境中，启用硬件驱动器：

```json
{
  "Driver": {
    "UseHardwareDriver": true,
    "Leadshine": {
      // 配置实际硬件参数
    }
  }
}
```

## 错误处理

硬件执行器会捕获所有设备异常，并转换为 `PathExecutionResult`：

- 设备通信失败 → 返回失败结果，包裹引导到异常格口
- 段执行超时 → 返回失败结果，使用TTL机制
- 操作取消 → 返回失败结果

## 性能考虑

- IO操作采用异步模式，避免阻塞线程
- 支持批量读写操作，提高效率
- 使用连接池管理设备连接
- TTL机制确保不会无限等待

## 故障排查

### 问题：找不到LTDMC.dll

**解决方案**：
- 确保LTDMC.dll已复制到输出目录
- 检查项目文件中的 `<CopyToOutputDirectory>` 配置
- 确认操作系统架构匹配（x64/x86）

### 问题：控制器连接失败

**解决方案**：
- 检查控制器是否上电
- 确认CardNo配置正确
- 验证网络/USB连接状态
- 查看系统日志获取详细错误信息

### 问题：摆轮不响应

**解决方案**：
- 确认OutputStartBit配置正确
- 检查硬件接线
- 验证IO端口映射
- 使用示波器或多用表测试输出信号

## 参考链接

- [ZakYip.Singulation - 参考项目](https://github.com/Hisoka6602/ZakYip.Singulation)
- [雷赛LTDMC控制器文档](https://www.leadshine.com/)
- [项目主README](../README.md)

## 许可证

待定

## 贡献

欢迎提交Issue和Pull Request！
