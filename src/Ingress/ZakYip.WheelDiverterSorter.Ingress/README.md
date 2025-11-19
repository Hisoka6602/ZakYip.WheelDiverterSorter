# ZakYip.WheelDiverterSorter.Ingress

IO传感器监听模块，负责感应包裹并触发包裹检测事件。

## ✅ 已完成功能特性

- ✅ **真实传感器驱动**：集成雷赛（Leadshine）控制器的光电传感器和激光传感器
- ✅ **模拟传感器支持**：光电传感器、激光传感器的模拟实现用于测试
- ✅ **工厂模式架构**：支持动态加载不同厂商的传感器
- ✅ **事件驱动架构**：传感器事件触发机制
- ✅ **包裹到达检测**：自动检测包裹并生成唯一ID
- ✅ **线程安全的包裹ID生成**：使用共享Random实例确保高并发环境下的唯一性
- ✅ **包裹去重机制**：基于时间窗口和ID历史的双重去重机制
- ✅ **防抖动机制**：可配置的时间窗口避免短时间内重复触发
- ✅ **位置信息记录**：记录包裹检测的时间和传感器位置
- ✅ **传感器健康监控**：实时监控传感器状态，故障检测和恢复通知
- ✅ **错误处理和报告**：传感器错误事件和自动报告机制
- ✅ **依赖注入支持**：易于集成到ASP.NET Core应用
- ✅ **松耦合设计**：便于扩展更多厂商

## 核心接口

### ISensor
传感器接口，定义传感器的基本行为：
- `StartAsync()` - 启动传感器监听
- `StopAsync()` - 停止传感器监听
- `SensorTriggered` - 传感器触发事件
- `SensorError` - 传感器错误事件

### ISensorFactory
传感器工厂接口，支持多厂商传感器的动态创建：
- `CreateSensors()` - 创建配置的所有传感器实例

### IParcelDetectionService
包裹检测服务接口，负责处理传感器事件并检测包裹：
- `StartAsync()` - 启动包裹检测服务
- `StopAsync()` - 停止包裹检测服务
- `ParcelDetected` - 包裹检测事件

### ISensorHealthMonitor
传感器健康监控服务接口，负责监控传感器健康状态：
- `StartAsync()` - 启动健康监控
- `StopAsync()` - 停止健康监控
- `GetHealthStatus()` - 获取传感器健康状态
- `GetAllHealthStatus()` - 获取所有传感器健康状态
- `ReportError()` - 手动报告传感器错误
- `SensorFault` - 传感器故障事件
- `SensorRecovery` - 传感器恢复事件

## 使用示例

### 1. 注册服务（在Program.cs中）

```csharp
using ZakYip.WheelDiverterSorter.Ingress;

// 使用扩展方法自动注册所有传感器服务
builder.Services.AddSensorServices(builder.Configuration);

// 这将自动注册：
// - ISensorFactory（根据配置选择Mock或Leadshine工厂）
// - IEnumerable<ISensor>（所有传感器实例）
// - IParcelDetectionService（包裹检测服务）
// - ISensorHealthMonitor（传感器健康监控服务）
```

### 2. 配置传感器（appsettings.json）

#### 使用模拟传感器（测试环境）

```json
{
  "Sensor": {
    "UseHardwareSensor": false,
    "MockSensors": [
      {
        "SensorId": "SENSOR_PE_01",
        "Type": "Photoelectric",
        "IsEnabled": true
      },
      {
        "SensorId": "SENSOR_LASER_01",
        "Type": "Laser",
        "IsEnabled": true
      }
    ]
  },
  "ParcelDetection": {
    "DeduplicationWindowMs": 1000,
    "ParcelIdHistorySize": 1000
  }
}
```

#### 使用雷赛硬件传感器（生产环境）

```json
{
  "Sensor": {
    "UseHardwareSensor": true,
    "VendorType": "Leadshine",
    "Leadshine": {
      "CardNo": 0,
      "Sensors": [
        {
          "SensorId": "SENSOR_PE_01",
          "Type": "Photoelectric",
          "InputBit": 0,
          "IsEnabled": true
        },
        {
          "SensorId": "SENSOR_LASER_01",
          "Type": "Laser",
          "InputBit": 1,
          "IsEnabled": true
        }
      ]
    }
  },
  "ParcelDetection": {
    "DeduplicationWindowMs": 1000,
    "ParcelIdHistorySize": 1000
  }
}
```

#### 包裹检测配置选项

- **DeduplicationWindowMs**: 去重时间窗口（毫秒）。在此时间窗口内，同一传感器的重复触发将被忽略。默认值：1000ms (1秒)
- **ParcelIdHistorySize**: 包裹ID历史记录最大数量。用于防止重复包裹ID，超过此数量将移除最旧的记录。默认值：1000

### 3. 使用包裹检测服务和健康监控

```csharp
public class MyService
{
    private readonly IParcelDetectionService _parcelDetectionService;
    private readonly ISensorHealthMonitor _healthMonitor;

    public MyService(
        IParcelDetectionService parcelDetectionService,
        ISensorHealthMonitor healthMonitor)
    {
        _parcelDetectionService = parcelDetectionService;
        _healthMonitor = healthMonitor;
    }

    public async Task StartMonitoringAsync()
    {
        // 订阅包裹检测事件
        _parcelDetectionService.ParcelDetected += OnParcelDetected;

        // 订阅传感器健康事件
        _healthMonitor.SensorFault += OnSensorFault;
        _healthMonitor.SensorRecovery += OnSensorRecovery;

        // 启动监听
        await _parcelDetectionService.StartAsync();
    }

    private void OnParcelDetected(object? sender, ParcelDetectedEventArgs e)
    {
        Console.WriteLine($"检测到包裹: {e.ParcelId}");
        Console.WriteLine($"  检测时间: {e.DetectedAt}");
        Console.WriteLine($"  检测位置: {e.Position} (传感器: {e.SensorId})");
        Console.WriteLine($"  传感器类型: {e.SensorType}");
        
        // 在这里处理包裹检测逻辑：
        // 1. 保存包裹记录到数据库
        // 2. 向RuleEngine请求格口号
        // 3. 执行分拣操作
    }

    private void OnSensorFault(object? sender, SensorFaultEventArgs e)
    {
        Console.WriteLine($"传感器故障: {e.SensorId} - {e.Description}");
        // 发送告警通知
    }

    private void OnSensorRecovery(object? sender, SensorRecoveryEventArgs e)
    {
        Console.WriteLine($"传感器恢复: {e.SensorId}");
    }

    public IDictionary<string, SensorHealthStatus> GetSensorStatus()
    {
        // 获取所有传感器的健康状态
        return _healthMonitor.GetAllHealthStatus();
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

sensor.SensorError += (sender, e) =>
{
    Console.WriteLine($"传感器错误: {e.SensorId}, 错误: {e.ErrorMessage}");
};

// 启动传感器
await sensor.StartAsync();

// 停止传感器
await sensor.StopAsync();
```

## 已实现的真实传感器驱动

### 雷赛（Leadshine）传感器

当前系统已集成雷赛控制器的传感器驱动：

- **LeadshinePhotoelectricSensor** - 雷赛光电传感器
  - 通过读取雷赛控制器IO输入端口检测包裹
  - 10ms轮询周期，快速响应
  - 自动状态变化检测
  - 完整的错误处理和报告

- **LeadshineLaserSensor** - 雷赛激光传感器
  - 同样基于雷赛控制器IO端口
  - 适用于需要更高精度的场景
  - 与光电传感器相同的接口和行为

- **LeadshineSensorFactory** - 雷赛传感器工厂
  - 根据配置自动创建传感器实例
  - 支持多个传感器同时工作
  - 统一的依赖注入和生命周期管理

**硬件要求**：
- 雷赛LTDMC系列运动控制卡
- Windows操作系统（LTDMC.dll支持）
- 正确的IO端口连接和配置

**参考项目**：
- [ZakYip.Singulation](https://github.com/Hisoka6602/ZakYip.Singulation) - 参考了其IO厂商和读写逻辑

## 传感器健康监控

### 功能特性

- ✅ **实时状态监控**：持续监控所有传感器的工作状态
- ✅ **故障检测**：自动检测传感器故障（连续错误、长时间无响应等）
- ✅ **故障告警**：通过事件通知传感器故障
- ✅ **自动恢复检测**：检测传感器从故障状态恢复
- ✅ **健康统计**：记录触发次数、错误次数、运行时长等指标
- ✅ **可配置阈值**：错误阈值、超时阈值等可配置

### 监控指标

每个传感器的健康状态包含以下信息：

- **IsHealthy** - 是否健康
- **LastTriggerTime** - 最后触发时间
- **LastCheckTime** - 最后检查时间
- **ErrorCount** - 错误计数
- **LastError** - 最后错误信息
- **LastErrorTime** - 最后错误时间
- **TotalTriggerCount** - 总触发次数
- **UptimeSeconds** - 运行时长（秒）
- **StartTime** - 启动时间

### 故障类型

- **CommunicationTimeout** - 通信超时
- **NoResponse** - 长时间无响应
- **ReadError** - 读取错误
- **DeviceOffline** - 设备离线
- **ConfigurationError** - 配置错误
- **Unknown** - 未知错误

## 模拟实现

当前提供的模拟实现用于测试和开发：

- **MockPhotoelectricSensor** - 模拟光电传感器
- **MockLaserSensor** - 模拟激光传感器
- **MockSensorFactory** - 模拟传感器工厂

这些模拟实现会随机生成传感器触发事件（5-15秒间隔），模拟包裹通过传感器的场景。

## 扩展新厂商传感器

系统采用**松耦合的工厂模式设计**，便于扩展其他厂商的传感器：

### 步骤1: 实现传感器类

```csharp
public class SiemensSensor : ISensor
{
    public string SensorId { get; }
    public SensorType Type { get; }
    public bool IsRunning { get; private set; }
    
    public event EventHandler<SensorEvent>? SensorTriggered;
    public event EventHandler<SensorErrorEventArgs>? SensorError;
    
    public Task StartAsync(CancellationToken cancellationToken = default) 
    {
        // 实现与西门子PLC的通信逻辑
    }
    
    public Task StopAsync() { }
    public void Dispose() { }
}
```

### 步骤2: 实现工厂类

```csharp
public class SiemensSensorFactory : ISensorFactory
{
    public IEnumerable<ISensor> CreateSensors()
    {
        // 根据配置创建西门子传感器实例
    }
}
```

### 步骤3: 添加配置支持

在 `SensorOptions.cs` 中添加新厂商配置类：

```csharp
public class SiemensSensorOptions
{
    public string IpAddress { get; set; }
    public int Port { get; set; }
    public List<SiemensSensorConfigDto> Sensors { get; set; } = new();
}
```

在 `SensorServiceExtensions.cs` 中添加注册逻辑：

```csharp
case "siemens":
    AddSiemensSensorServices(services, sensorOptions);
    break;
```

### 优势

- ✅ **低耦合**：通过接口和工厂模式实现解耦
- ✅ **易扩展**：添加新厂商只需实现接口，无需修改现有代码
- ✅ **可配置**：通过配置文件切换传感器厂商
- ✅ **统一接口**：所有传感器使用相同的接口，业务逻辑无需改变

## 生产环境部署

### 硬件要求

- 雷赛LTDMC系列运动控制卡
- Windows操作系统
- 传感器正确接线到控制卡IO端口

### 配置步骤

1. 安装雷赛控制卡驱动程序
2. 确认控制卡号（通常为0）
3. 确认传感器连接的IO端口号
4. 修改 `appsettings.json` 配置
5. 设置 `UseHardwareSensor: true`
6. 配置传感器InputBit
7. 重启应用程序

### 故障排查

**问题：传感器无响应**
- 检查IO端口接线
- 确认InputBit配置正确
- 查看日志中的错误信息
- 使用雷赛官方软件测试IO端口

**问题：频繁报错**
- 检查控制卡连接
- 确认驱动程序已安装
- 检查传感器电源供应
- 查看健康监控状态

## 与系统其他部分的集成

1. **与RuleEngine通信**：包裹检测后，应通过TCP/SignalR/MQTT向RuleEngine请求格口号
2. **与Core层集成**：接收格口号后，调用 `ISwitchingPathGenerator` 生成路径
3. **与Execution层集成**：调用 `ISwitchingPathExecutor` 执行分拣动作

完整流程：
```
传感器触发 → 包裹检测 → 生成包裹ID → 请求格口号 → 生成路径 → 执行分拣
```
