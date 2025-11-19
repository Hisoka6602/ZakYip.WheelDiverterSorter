# 传感器工厂模式说明

## 概述

传感器层采用**工厂模式实现低耦合架构**，便于扩展多种传感器厂商。

## 架构设计

```
┌──────────────────────────────────────────────┐
│           ISensor (接口)                      │
│   - StartAsync()                             │
│   - StopAsync()                              │
│   - SensorTriggered (事件)                   │
└────────────────┬─────────────────────────────┘
                 │
     ┌───────────┴───────────┐
     │  ISensorFactory       │
     │  CreateSensors()      │
     └───────────┬───────────┘
                 │
     ┌───────────┴──────────────────┬────────────┐
     │                              │            │
┌────▼────────────┐  ┌─────────────▼──┐  ┌─────▼──────┐
│Leadshine        │  │Mock            │  │其他厂商     │
│SensorFactory    │  │SensorFactory   │  │(易于扩展)   │
└─────┬───────────┘  └────────┬───────┘  └────────────┘
      │                       │
      ├─ Photoelectric        ├─ MockPhotoelectric
      └─ Laser                └─ MockLaser
```

## 扩展新厂商传感器

### 步骤1: 实现传感器类

```csharp
public class SiemensSensor : ISensor
{
    public string SensorId { get; }
    public SensorType Type { get; }
    public bool IsRunning { get; private set; }
    public event EventHandler<SensorEvent>? SensorTriggered;
    
    // 实现接口方法
    public Task StartAsync(CancellationToken cancellationToken = default) { }
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
        // 根据配置创建传感器实例
    }
}
```

### 步骤3: 添加配置

在 `SensorOptions.cs` 中添加新厂商配置类，并在 `SensorServiceExtensions.cs` 中添加注册逻辑。

## 配置示例

### 使用模拟传感器（测试环境）

```json
{
  "Sensor": {
    "UseHardwareSensor": false,
    "MockSensors": [
      {
        "SensorId": "SENSOR_PE_01",
        "Type": "Photoelectric",
        "IsEnabled": true
      }
    ]
  }
}
```

### 使用雷赛硬件传感器（生产环境）

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
        }
      ]
    }
  }
}
```

## 优势

- ✅ **低耦合**：通过接口和工厂模式实现解耦
- ✅ **易扩展**：添加新厂商只需实现接口，无需修改现有代码
- ✅ **可配置**：通过配置文件切换传感器厂商
- ✅ **统一接口**：所有传感器使用相同的接口，业务逻辑无需改变
