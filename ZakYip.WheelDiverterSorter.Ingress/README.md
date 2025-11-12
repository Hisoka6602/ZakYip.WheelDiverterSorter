# ZakYip.WheelDiverterSorter.Ingress

IO传感器监听模块，负责感应包裹并触发包裹检测事件。

## 功能特性

- ✅ **多种传感器支持**：光电传感器、激光传感器
- ✅ **事件驱动架构**：传感器事件触发机制
- ✅ **包裹到达检测**：自动检测包裹并生成唯一ID
- ✅ **防抖动机制**：避免短时间内重复触发
- ✅ **依赖注入支持**：易于集成到ASP.NET Core应用

## 核心接口

### ISensor
传感器接口，定义传感器的基本行为：
- `StartAsync()` - 启动传感器监听
- `StopAsync()` - 停止传感器监听
- `SensorTriggered` - 传感器触发事件

### IParcelDetectionService
包裹检测服务接口，负责处理传感器事件并检测包裹：
- `StartAsync()` - 启动包裹检测服务
- `StopAsync()` - 停止包裹检测服务
- `ParcelDetected` - 包裹检测事件

## 使用示例

### 1. 注册服务（在Program.cs中）

```csharp
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Sensors;
using ZakYip.WheelDiverterSorter.Ingress.Services;

// 注册传感器
builder.Services.AddSingleton<ISensor>(sp => new MockPhotoelectricSensor("SENSOR_PE_01"));
builder.Services.AddSingleton<ISensor>(sp => new MockLaserSensor("SENSOR_LASER_01"));

// 注册包裹检测服务
builder.Services.AddSingleton<IParcelDetectionService, ParcelDetectionService>();

// 可选：注册后台服务以自动启动传感器监听
builder.Services.AddHostedService<SensorMonitoringWorker>();
```

### 2. 使用包裹检测服务

```csharp
public class MyService
{
    private readonly IParcelDetectionService _parcelDetectionService;

    public MyService(IParcelDetectionService parcelDetectionService)
    {
        _parcelDetectionService = parcelDetectionService;
    }

    public async Task StartMonitoringAsync()
    {
        // 订阅包裹检测事件
        _parcelDetectionService.ParcelDetected += OnParcelDetected;

        // 启动监听
        await _parcelDetectionService.StartAsync();
    }

    private void OnParcelDetected(object? sender, ParcelDetectedEventArgs e)
    {
        Console.WriteLine($"检测到包裹: {e.ParcelId}");
        
        // 在这里处理包裹检测逻辑：
        // 1. 保存包裹记录到数据库
        // 2. 向RuleEngine请求格口号
        // 3. 执行分拣操作
    }
}
```

### 3. 手动使用传感器

```csharp
// 创建传感器
var sensor = new MockPhotoelectricSensor("SENSOR_001");

// 订阅事件
sensor.SensorTriggered += (sender, e) =>
{
    Console.WriteLine($"传感器触发: {e.SensorId}, 状态: {e.IsTriggered}");
};

// 启动传感器
await sensor.StartAsync();

// 停止传感器
await sensor.StopAsync();
```

## 模拟实现

当前提供的实现为模拟版本，用于测试和开发：

- **MockPhotoelectricSensor** - 模拟光电传感器
- **MockLaserSensor** - 模拟激光传感器

这些模拟实现会随机生成传感器触发事件（5-15秒间隔），模拟包裹通过传感器的场景。

## 生产环境部署

在生产环境中，需要将模拟实现替换为真实的硬件通信实现：

1. 创建继承自 `ISensor` 的真实传感器类
2. 实现与PLC或传感器硬件的通信逻辑
3. 在依赖注入中注册真实传感器实现

示例：

```csharp
public class RealPhotoelectricSensor : ISensor
{
    // 实现与真实光电传感器的通信
    // 例如：通过串口、ModbusTCP、OPC UA等协议
}

// 注册真实传感器
builder.Services.AddSingleton<ISensor>(sp => 
    new RealPhotoelectricSensor("SENSOR_PE_01", "COM1"));
```

## 配置管理

传感器配置模型（SensorConfiguration）支持：
- 传感器ID
- 传感器类型
- 端口或地址
- 启用/禁用状态
- 灵敏度设置

可以从配置文件或数据库加载传感器配置，实现动态管理。

## 与系统其他部分的集成

1. **与RuleEngine通信**：包裹检测后，应通过TCP/SignalR/MQTT向RuleEngine请求格口号
2. **与Core层集成**：接收格口号后，调用 `ISwitchingPathGenerator` 生成路径
3. **与Execution层集成**：调用 `ISwitchingPathExecutor` 执行分拣动作

完整流程：
```
传感器触发 → 包裹检测 → 生成包裹ID → 请求格口号 → 生成路径 → 执行分拣
```
