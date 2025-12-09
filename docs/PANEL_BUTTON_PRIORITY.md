# 面板按钮优先级说明

## 概述

本文档说明IO启动按钮预警期间，如何通过停止或急停按钮立即取消预警并响应高优先级操作。

## 按钮优先级

系统实现了以下按钮优先级机制（从高到低）：

1. **急停按钮** (EmergencyStop) - 最高优先级
2. **停止按钮** (Stop) - 第二优先级
3. **启动按钮** (Start) - 最低优先级

## 工作原理

### 启动按钮预警机制

当按下启动按钮时，系统会：
1. 保持在当前状态（Ready/Paused）
2. 激活预警输出（如果配置了 PreStartWarningOutputBit）
3. 等待配置的预警时间（PreStartWarningDurationSeconds）
4. 预警时间结束后，转换到 Running 状态

### 高优先级按钮中断机制

**在预警等待期间**，如果按下停止或急停按钮：

1. **立即取消预警等待**
   - 预警等待会被中断
   - 不会转换到 Running 状态
   
2. **关闭预警输出**
   - 确保预警输出被正确关闭
   
3. **由高优先级按钮处理状态转换**
   - 停止按钮：转到 Ready/Stopped 状态
   - 急停按钮：转到 EmergencyStop 状态

## 配置

面板配置中的相关参数：

```csharp
// 面板配置
public class PanelConfiguration
{
    // 预警时间（秒），0 表示无预警
    public int? PreStartWarningDurationSeconds { get; set; }
    
    // 预警输出位
    public int? PreStartWarningOutputBit { get; set; }
    
    // 预警输出触发电平
    public TriggerLevel PreStartWarningOutputLevel { get; set; }
}
```

## 使用场景

### 场景1: 正常启动流程
```
1. 操作员按下启动按钮
2. 系统开始3秒预警（蜂鸣器响起）
3. 3秒后预警结束
4. 系统转到 Running 状态
5. 摆轮开始运行
```

### 场景2: 预警期间取消启动
```
1. 操作员按下启动按钮
2. 系统开始3秒预警（蜂鸣器响起）
3. 1秒后，操作员按下停止按钮
4. 预警立即取消（蜂鸣器停止）
5. 系统保持在 Ready 状态
6. 启动被取消
```

### 场景3: 预警期间紧急停止
```
1. 操作员按下启动按钮
2. 系统开始3秒预警
3. 发现异常，操作员立即按下急停按钮
4. 预警立即取消
5. 系统进入 EmergencyStop 状态
6. 所有设备紧急停止
```

## 日志示例

### 正常预警完成
```
[INFO] 启动按钮处理开始 - 当前状态: Ready, 配置的预警时间: 3 秒
[WARN] ⚠️ 启动按钮按下，开始预警 3 秒，当前状态保持为 Ready，摆轮将在预警结束后启动
[INFO] 开始等待预警时间: 3 秒...
[WARN] ✅ 预警时间结束，实际等待: 3.00 秒，准备转换到 Running 状态并启动摆轮
[INFO] 正在将系统状态从 Ready 转换到 Running...
[INFO] ✅ 系统状态已成功转换到 Running，摆轮应该开始启动
```

### 预警被停止按钮取消
```
[INFO] 启动按钮处理开始 - 当前状态: Ready, 配置的预警时间: 3 秒
[WARN] ⚠️ 启动按钮按下，开始预警 3 秒，当前状态保持为 Ready，摆轮将在预警结束后启动
[INFO] 开始等待预警时间: 3 秒...
[INFO] 触发按钮 Stop 的IO联动，当前系统状态：Ready
[WARN] 检测到高优先级按钮 Stop，取消正在进行的启动预警等待
[WARN] ⚠️ 预警等待被高优先级按钮（停止/急停）取消，实际等待: 0.85 秒
[INFO] 关闭预警输出: Bit=100, Level=ActiveHigh, Value=False
```

### 预警被急停按钮取消
```
[INFO] 启动按钮处理开始 - 当前状态: Ready, 配置的预警时间: 3 秒
[WARN] ⚠️ 启动按钮按下，开始预警 3 秒，当前状态保持为 Ready，摆轮将在预警结束后启动
[INFO] 开始等待预警时间: 3 秒...
[INFO] 触发按钮 EmergencyStop 的IO联动，当前系统状态：Ready
[WARN] 检测到高优先级按钮 EmergencyStop，取消正在进行的启动预警等待
[WARN] ⚠️ 预警等待被高优先级按钮（停止/急停）取消，实际等待: 1.23 秒
[INFO] 关闭预警输出: Bit=100, Level=ActiveHigh, Value=False
```

## 技术细节

### 线程安全
- 使用 `CancellationTokenSource` 实现可取消的预警等待
- 使用 `lock` 保护取消令牌源的访问
- 在 `finally` 块中确保资源正确释放

### 取消机制
```csharp
// 创建可取消的预警等待令牌
lock (_preWarningLock)
{
    _preWarningCancellationSource = new CancellationTokenSource();
    linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken,
        _preWarningCancellationSource.Token);
}

// 高优先级按钮取消预警
lock (_preWarningLock)
{
    if (_preWarningCancellationSource != null && !_preWarningCancellationSource.IsCancellationRequested)
    {
        _preWarningCancellationSource.Cancel();
    }
}
```

## 测试

相关测试位于：
- `tests/ZakYip.WheelDiverterSorter.E2ETests/PanelButtonStateSimulationTests.cs`
- `tests/ZakYip.WheelDiverterSorter.Core.Tests/DefaultPanelIoCoordinatorTests.cs`

## 参考

- [Issue Description]: 按下IO启动按钮的等待/准备阶段时按下停止也一样生效
- [Implementation]: `src/Host/ZakYip.WheelDiverterSorter.Host/Services/Workers/PanelButtonMonitorWorker.cs`
