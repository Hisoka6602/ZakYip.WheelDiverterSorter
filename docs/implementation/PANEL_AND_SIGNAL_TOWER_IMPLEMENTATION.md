# 电柜操作面板与三色状态灯功能实现文档

## 概述

本文档描述了 ZakYip.WheelDiverterSorter 项目中电柜操作面板与三色状态灯功能的实现。该实现参考了 ZakYip.Singulation 项目的设计风格，提供了完整的面板按钮控制、三色灯状态显示和仿真支持。

## 架构设计

### 分层结构

实现严格遵守项目现有的分层架构：

```
Core Layer (领域模型)
  ├─ 枚举类型 (PanelButtonType, SignalTowerChannel, SystemOperatingState)
  ├─ 状态模型 (PanelButtonState, SignalTowerState)
  ├─ 抽象接口 (IPanelInputReader, ISignalTowerOutput, IPanelIoCoordinator)
  ├─ 默认实现 (DefaultPanelIoCoordinator)
  └─ 配置选项 (PanelIoOptions, SignalTowerOptions)

Drivers Layer (硬件/仿真驱动)
  ├─ Simulated/
  │   ├─ SimulatedPanelInputReader (仿真面板输入)
  │   └─ SimulatedSignalTowerOutput (仿真信号塔输出)
  └─ (未来扩展: EmcPanelIoDriver 真实硬件驱动)

Host Layer (API与配置)
  └─ Controllers/
      └─ PanelSimulationController (仿真控制 API)
```

## 核心组件

### 1. 枚举类型

#### PanelButtonType（面板按钮类型）
- `Start` - 启动按钮
- `Stop` - 停止按钮
- `Reset` - 复位按钮
- `EmergencyStop` - 急停按钮
- `ModeAuto` - 自动模式选择
- `ModeManual` - 手动模式选择

#### SignalTowerChannel（信号塔通道）
- `Red` - 红色灯
- `Yellow` - 黄色灯
- `Green` - 绿色灯
- `Buzzer` - 蜂鸣器

#### SystemOperatingState（系统运行状态）
- `Initializing` - 初始化中
- `Standby` - 待机
- `Running` - 运行中
- `Paused` - 暂停
- `Stopping` - 停止中
- `Stopped` - 已停止
- `Faulted` - 故障
- `EmergencyStopped` - 急停
- `WaitingUpstream` - 等待上游

### 2. 状态模型

#### PanelButtonState（面板按钮状态）
使用 `readonly record struct` 实现不可变状态：
```csharp
public readonly record struct PanelButtonState
{
    public required PanelButtonType ButtonType { get; init; }
    public required bool IsPressed { get; init; }
    public required DateTimeOffset LastChangedAt { get; init; }
    public int PressedDurationMs { get; init; }
}
```

#### SignalTowerState（信号塔状态）
```csharp
public readonly record struct SignalTowerState
{
    public required SignalTowerChannel Channel { get; init; }
    public required bool IsActive { get; init; }
    public bool IsBlinking { get; init; }
    public int BlinkIntervalMs { get; init; }
    public int DurationMs { get; init; }
}
```

提供便捷的静态工厂方法：
- `CreateOn(channel)` - 创建点亮状态
- `CreateOff(channel)` - 创建熄灭状态
- `CreateBlinking(channel, intervalMs)` - 创建闪烁状态

### 3. 抽象接口

#### IPanelInputReader
负责读取面板按钮状态：
```csharp
Task<PanelButtonState> ReadButtonStateAsync(PanelButtonType buttonType, CancellationToken cancellationToken = default);
Task<IDictionary<PanelButtonType, PanelButtonState>> ReadAllButtonStatesAsync(CancellationToken cancellationToken = default);
```

#### ISignalTowerOutput
负责控制信号塔输出：
```csharp
Task SetChannelStateAsync(SignalTowerState state, CancellationToken cancellationToken = default);
Task SetChannelStatesAsync(IEnumerable<SignalTowerState> states, CancellationToken cancellationToken = default);
Task TurnOffAllAsync(CancellationToken cancellationToken = default);
Task<IDictionary<SignalTowerChannel, SignalTowerState>> GetAllChannelStatesAsync(CancellationToken cancellationToken = default);
```

#### IPanelIoCoordinator
协调系统状态与信号塔显示：
```csharp
IEnumerable<SignalTowerState> DetermineSignalTowerStates(
    SystemOperatingState systemState,
    bool hasAlarms,
    bool upstreamConnected);

bool IsButtonOperationAllowed(PanelButtonType buttonType, SystemOperatingState systemState);
```

### 4. 默认协调器实现

`DefaultPanelIoCoordinator` 实现了完整的状态映射逻辑：

| 系统状态 | 信号塔显示 | 描述 |
|---------|-----------|------|
| Initializing | 黄灯闪烁 | 系统正在初始化 |
| Standby | 黄灯常亮 | 系统待机，等待启动 |
| Running | 绿灯常亮 | 系统正常运行 |
| Running (无上游) | 绿灯 + 黄灯闪烁 | 运行但上游断开 |
| Paused | 绿灯闪烁 + 黄灯闪烁 | 系统暂停 |
| Stopping | 黄灯快速闪烁 | 系统停止中 |
| Stopped | 黄灯常亮 | 系统已停止 |
| Faulted | 红灯闪烁 + 蜂鸣器闪烁 | 系统故障 |
| EmergencyStopped | 红灯常亮 + 蜂鸣器常亮 | 急停状态 |
| WaitingUpstream | 黄灯慢速闪烁 | 等待上游响应 |

## 仿真实现

### SimulatedPanelInputReader
- 通过 `ConcurrentDictionary` 在内存中维护按钮状态
- 提供 `SimulatePressButton` 和 `SimulateReleaseButton` 方法用于测试
- 记录按钮按下时长

### SimulatedSignalTowerOutput
- 在内存中维护所有通道状态
- 记录状态变更历史，便于测试验证
- 提供 `GetStateChangeHistory()` 和 `ClearHistory()` 用于测试

## API 端点

### PanelSimulationController

提供以下仿真控制 API（仅在仿真模式下可用）：

#### POST /api/simulation/panel/press
模拟按下指定按钮。
```json
Query: buttonType=Start|Stop|Reset|EmergencyStop|ModeAuto|ModeManual
```

#### POST /api/simulation/panel/release
模拟释放指定按钮。
```json
Query: buttonType=Start|Stop|Reset|EmergencyStop|ModeAuto|ModeManual
```

#### GET /api/simulation/panel/state
获取当前面板状态（所有按钮和信号塔状态）。

#### POST /api/simulation/panel/reset
重置所有按钮状态。

#### GET /api/simulation/panel/signal-tower/history
获取信号塔状态变更历史。

## 配置选项

### PanelIoOptions
```csharp
public sealed record class PanelIoOptions
{
    public bool Enabled { get; init; } = false;
    public bool UseSimulation { get; init; } = true;
    public int PollingIntervalMs { get; init; } = 100;
    public int DebounceMs { get; init; } = 50;
}
```

### SignalTowerOptions
```csharp
public sealed record class SignalTowerOptions
{
    public bool Enabled { get; init; } = false;
    public int DefaultBlinkIntervalMs { get; init; } = 500;
    public int BuzzerMaxDurationMs { get; init; } = 10000;
    public bool TestAllChannelsOnStartup { get; init; } = true;
    public int StartupTestDurationMs { get; init; } = 500;
}
```

## 测试覆盖

### 单元测试（21 tests）

#### Core 层（10 tests）
- `DefaultPanelIoCoordinator` 的状态映射逻辑
- 按钮操作权限验证
- 不同系统状态下的信号塔显示

#### Drivers 层（11 tests）
- `SimulatedPanelInputReader` 按钮状态读取
- `SimulatedSignalTowerOutput` 通道控制
- 状态历史记录功能

### 集成测试（5 tests）

#### E2E 场景测试
1. **基础操作流** - 待机 → 启动 → 运行 → 停止 → 复位
2. **故障场景** - 故障触发红灯闪烁和蜂鸣器，复位后恢复
3. **急停场景** - 紧急停止触发红灯常亮，禁止重复急停
4. **上游断开警告** - 运行时上游断开，同时显示绿灯和闪烁黄灯
5. **完整工作流** - 完整状态转换历史追踪

### 测试结果
- **总计**: 26 tests
- **通过**: 26 tests
- **失败**: 0 tests
- **覆盖率**: Core 和 Drivers 层新增代码 100%

## 使用示例

### 1. 配置仿真模式（appsettings.json）
```json
{
  "PanelIo": {
    "Enabled": true,
    "UseSimulation": true,
    "PollingIntervalMs": 100,
    "DebounceMs": 50
  },
  "SignalTower": {
    "Enabled": true,
    "DefaultBlinkIntervalMs": 500,
    "BuzzerMaxDurationMs": 10000
  }
}
```

### 2. 依赖注入配置（Program.cs）
```csharp
// 注册仿真实现
services.AddSingleton<IPanelInputReader, SimulatedPanelInputReader>();
services.AddSingleton<ISignalTowerOutput, SimulatedSignalTowerOutput>();
services.AddSingleton<IPanelIoCoordinator, DefaultPanelIoCoordinator>();

// 或注册真实硬件实现
// services.AddSingleton<IPanelInputReader, EmcPanelIoDriver>();
// services.AddSingleton<ISignalTowerOutput, EmcPanelIoDriver>();
```

### 3. 在业务代码中使用
```csharp
public class SystemController
{
    private readonly IPanelInputReader _panelReader;
    private readonly ISignalTowerOutput _signalTower;
    private readonly IPanelIoCoordinator _coordinator;

    public async Task UpdateSystemStateAsync(SystemOperatingState newState)
    {
        // 读取按钮状态
        var buttonStates = await _panelReader.ReadAllButtonStatesAsync();
        
        // 根据系统状态确定信号塔显示
        var signalStates = _coordinator.DetermineSignalTowerStates(
            newState, 
            hasAlarms: CheckAlarms(),
            upstreamConnected: CheckUpstreamConnection()
        );
        
        // 更新信号塔
        await _signalTower.TurnOffAllAsync();
        await _signalTower.SetChannelStatesAsync(signalStates);
    }
}
```

## 设计原则

1. **零侵入原则**
   - 新功能通过接口和 DI 注入，不修改现有核心业务逻辑
   - 可以随时启用或禁用面板功能

2. **仿真与生产一致**
   - 仿真和真实硬件共用相同的业务逻辑代码
   - 仅通过 DI 配置切换不同的驱动实现

3. **不可变状态模型**
   - 使用 `readonly record struct` 确保状态不被意外修改
   - 所有状态变更创建新对象而非修改现有对象

4. **清晰的职责分离**
   - `IPanelInputReader` - 只负责读取输入
   - `ISignalTowerOutput` - 只负责控制输出
   - `IPanelIoCoordinator` - 只负责状态映射逻辑

5. **完整的测试覆盖**
   - 单元测试覆盖所有核心逻辑
   - 集成测试验证完整工作流
   - 仿真驱动便于自动化测试

## 未来扩展

### 短期计划
1. 实现真实硬件驱动 `EmcPanelIoDriver`
2. 在 Execution 层集成面板按钮处理逻辑
3. 添加面板操作的事件发布机制

### 长期计划
1. 支持更多按钮类型和自定义按钮
2. 支持可配置的状态映射规则
3. 添加面板操作审计日志
4. 支持远程面板控制（通过 SignalR）

## 参考文档

- [copilot-instructions.md](copilot-instructions.md) - 项目编码规范
- [README.md](README.md) - 项目概述和运行说明
- [HARDWARE_DRIVER_CONFIG.md](HARDWARE_DRIVER_CONFIG.md) - 硬件驱动配置

## 版本历史

- **v1.0.0** (2025-11-16)
  - 初始实现
  - Core 层领域模型和接口
  - Drivers 层仿真实现
  - Host 层 API 端点
  - 完整测试覆盖（26 tests）
