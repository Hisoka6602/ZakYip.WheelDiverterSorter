# 系统"影子状态"诊断与修复指南

## 问题描述

**症状**：系统看起来已启动，但包裹创建时仍然没有向上游发送信息。

## 根本原因：状态拦截机制

代码中存在**状态拦截机制**（不是Bug，是设计）：

```csharp
// SortingOrchestrator.ValidateSystemStateAsync() - 第570-585行
private Task<(bool IsValid, string? Reason)> ValidateSystemStateAsync(long parcelId)
{
    var currentState = _systemStateManager.CurrentState;
    if (!currentState.AllowsParcelCreation())  // 第573行
    {
        var errorMessage = currentState.GetParcelCreationDeniedMessage();
        _logger.LogWarning(
            "包裹 {ParcelId} 被拒绝：{ErrorMessage}",
            parcelId,
            errorMessage);
        
        return Task.FromResult((IsValid: false, Reason: (string?)errorMessage));
    }
    
    return Task.FromResult((IsValid: true, Reason: (string?)null));
}
```

**核心规则**：
```csharp
// SystemStateExtensions.AllowsParcelCreation() - 第21-24行
public static bool AllowsParcelCreation(this SystemState state)
{
    return state == SystemState.Running;  // 只有 Running 状态允许创建包裹
}
```

## 系统状态枚举

```csharp
public enum SystemState
{
    Booting = 0,        // 启动中：系统正在启动和初始化
    Ready = 1,          // 就绪：系统已就绪，可以开始运行
    Running = 2,        // 运行中：系统正常运行，执行分拣任务 ✅ 唯一允许创建包裹的状态
    Paused = 3,         // 暂停：系统已暂停，可恢复运行
    Faulted = 4,        // 故障：系统发生故障，需要处理
    EmergencyStop = 5   // 急停：触发急停按钮，系统紧急停止
}
```

## "影子状态"场景分析

### 场景 1: 系统卡在 Booting 状态

**可能原因**：
- 启用了健康检查（`HealthCheck:Enabled = true`）
- 系统初始化为 `Booting` 状态（第68-70行）
- 自检未完成或失败，未转换到 `Ready` 状态

**检查方法**：
```bash
GET /api/system/status
```

**期望响应**：
```json
{
  "currentState": "Booting",  // ❌ 不是 Running
  "lastSelfTestReport": {
    "overallSuccess": false
  }
}
```

**解决方法**：
1. 检查自检日志，找到失败原因
2. 修复自检失败的组件（通常是硬件连接或配置问题）
3. 重启服务或手动切换到 Ready 状态
4. 从 Ready 启动到 Running 状态

### 场景 2: 系统在 Ready 状态未启动

**可能原因**：
- 系统已初始化到 `Ready` 状态
- 但未通过面板或 API 启动到 `Running` 状态

**检查方法**：
```bash
GET /api/system/status
```

**期望响应**：
```json
{
  "currentState": "Ready",  // ❌ 不是 Running
  "isStarted": false
}
```

**解决方法**：
```bash
# 方法1: 通过 API 启动
POST /api/system/start

# 方法2: 通过面板按钮
# 按下"启动"按钮
```

### 场景 3: 系统在 Paused 状态

**可能原因**：
- 系统曾经运行，但被暂停
- 暂停状态下不允许创建包裹

**检查方法**：
```bash
GET /api/system/status
```

**期望响应**：
```json
{
  "currentState": "Paused",  // ❌ 不是 Running
  "isStarted": true
}
```

**解决方法**：
```bash
# 恢复运行
POST /api/system/resume

# 或者停止后重新启动
POST /api/system/stop
POST /api/system/start
```

### 场景 4: 系统在 Faulted 状态

**可能原因**：
- 系统检测到故障
- 故障状态下不允许创建包裹

**检查方法**：
```bash
GET /api/system/status
```

**期望响应**：
```json
{
  "currentState": "Faulted",  // ❌ 不是 Running
  "errorMessage": "..."
}
```

**解决方法**：
1. 查看日志找到故障原因
2. 修复故障（硬件、配置、网络等）
3. 恢复到 Ready 状态
4. 重新启动到 Running 状态

## 状态转移规则

```
Booting → Ready（启动完成/自检通过）
Ready → Running（启动系统）⭐ 必须执行这一步
Running → Paused（暂停系统）
Paused → Running（恢复运行）
Running/Paused/Ready → Ready（停止系统）
任何状态 → EmergencyStop（急停）
EmergencyStop → Ready（急停解除）
任何状态 → Faulted（故障发生）
Faulted → Ready（故障恢复）
```

## 完整诊断流程

### 步骤 1: 检查当前状态

```bash
GET /api/system/status
```

**重点关注**：
- `currentState` 字段：必须是 `"Running"` 才能创建包裹
- `isStarted` 字段：应为 `true`
- `lastSelfTestReport` 字段：如果启用自检，检查是否全部通过

### 步骤 2: 查看状态转移历史

```bash
GET /api/system/transitions
# 或查看日志中的状态转移记录
```

**查找关键信息**：
- 系统是否曾经到达 `Running` 状态
- 是否有异常的状态跳转
- 是否有失败的状态转移

### 步骤 3: 启用详细日志

在 `appsettings.json` 中：

```json
{
  "Logging": {
    "LogLevel": {
      "ZakYip.WheelDiverterSorter.Host.StateMachine": "Debug",
      "ZakYip.WheelDiverterSorter.Execution.Orchestration.SortingOrchestrator": "Debug"
    }
  }
}
```

### 步骤 4: 查找状态拦截日志

在日志中搜索以下关键字：

```
"包裹 {ParcelId} 被拒绝"
"系统当前状态 {state} 不允许创建包裹"
"系统当前未处于运行状态，禁止创建包裹"
```

**示例日志**：
```
[警告] 包裹 123456 被拒绝：系统当前未处于运行状态，禁止创建包裹。当前状态: 就绪
```

这表明系统在 `Ready` 状态，需要启动到 `Running` 状态。

### 步骤 5: 确认事件处理流程

检查日志中的完整流程：

```
[调试] 收到 ParcelDetected 事件: ParcelId=xxx
    ↓
[信息] [ParcelCreation触发] 检测到包裹创建传感器触发
    ↓
[警告] 包裹 xxx 被拒绝：系统当前未处于运行状态  ⬅️ 在这里被拦截
    ↓
❌ 没有后续的"创建包裹"和"发送上游通知"日志
```

## 修复方案

### 方案 1: 通过 API 启动系统

```bash
# 1. 检查当前状态
GET /api/system/status

# 2. 如果在 Ready 状态，启动到 Running
POST /api/system/start

# 3. 确认状态已变为 Running
GET /api/system/status

# 4. 触发传感器或等待包裹到达
# 现在应该能看到上游通知了
```

### 方案 2: 通过面板启动系统

1. 查看系统面板
2. 确认当前显示状态（Ready/Booting/Paused/Faulted）
3. 按下"启动"按钮
4. 等待状态变为"运行中"
5. 触发传感器测试

### 方案 3: 禁用健康检查（如果卡在 Booting）

在 `appsettings.json` 中：

```json
{
  "HealthCheck": {
    "Enabled": false  // 禁用自检，直接从 Ready 启动
  }
}
```

重启服务后，系统会直接初始化到 `Ready` 状态，然后可以启动到 `Running`。

### 方案 4: 排查并修复自检失败

如果启用了健康检查且卡在 `Booting` 状态：

1. 查看日志中的自检报告：
   ```
   [信息] 系统自检开始...
   [错误] 硬件自检失败: xxx
   ```

2. 修复失败的组件：
   - 硬件连接问题
   - 配置错误
   - 驱动未就绪

3. 重启服务或手动切换状态：
   ```bash
   POST /api/system/reset  # 重置到 Ready
   POST /api/system/start  # 启动到 Running
   ```

## 为什么 test-parcel 仍然能发送？

`test-parcel` 端点有**不同的状态要求**：

```csharp
// CommunicationController.SendTestParcel() - 第789-798行
var currentState = _stateManager.CurrentState;
if (currentState != SystemState.Ready && 
    currentState != SystemState.Faulted) {  // ⬅️ 只允许 Ready 或 Faulted
    return BadRequest(new StateValidationErrorResponse {
        Message = "系统当前状态不允许发送测试包裹",
        CurrentState = currentState.ToString(),
        RequiredState = "Ready or Faulted"
    });
}
```

**对比**：

| 特征 | 正常包裹创建 | test-parcel 端点 |
|------|-------------|------------------|
| 允许的状态 | **Running** | **Ready** 或 **Faulted** |
| 用途 | 生产环境分拣 | 通信测试 |
| 创建包裹实体 | ✅ | ❌ |

**结论**：`test-parcel` 设计为在系统**未运行时**测试通信，所以它允许 `Ready` 状态。而正常包裹创建必须在 `Running` 状态。

## 验证清单

使用此清单确认问题已解决：

- [ ] 系统状态为 `Running`（`GET /api/system/status`）
- [ ] 传感器服务已启动（日志确认）
- [ ] 上游连接正常（`GET /api/communication/status`）
- [ ] 日志级别设置为 Debug
- [ ] 触发传感器后，日志中出现：
  - [ ] `[调试] 收到 ParcelDetected 事件`
  - [ ] `[信息] [ParcelCreation触发] 检测到包裹创建传感器触发`
  - [ ] `[跟踪] [PR-42 Parcel-First] 本地创建包裹`
  - [ ] `[信息] [PR-42 Parcel-First] 发送上游包裹检测通知`
  - [ ] `[信息] 包裹 xxx 已成功发送检测通知到上游系统`
- [ ] **没有** `[警告] 包裹 xxx 被拒绝` 日志

## 快速排查命令

```bash
# 一键诊断脚本
echo "=== 系统状态检查 ==="
curl -s http://localhost:5000/api/system/status | jq '.currentState'

echo "=== 通信状态检查 ==="
curl -s http://localhost:5000/api/communication/status | jq '.isConnected'

echo "=== 如果不是 Running，执行启动 ==="
curl -X POST http://localhost:5000/api/system/start

echo "=== 再次检查状态 ==="
curl -s http://localhost:5000/api/system/status | jq '.currentState'
```

## 相关代码位置

- **状态验证逻辑**：`SortingOrchestrator.ValidateSystemStateAsync()` - 第570-585行
- **允许包裹创建规则**：`SystemStateExtensions.AllowsParcelCreation()` - 第21-24行
- **状态管理器注册**：`WheelDiverterSorterHostServiceCollectionExtensions.cs` - 第68-75行
- **test-parcel状态检查**：`CommunicationController.SendTestParcel()` - 第789-798行

## 总结

**"影子状态"的本质**：系统可能已启动服务进程，但**系统状态机**仍处于非 `Running` 状态（如 `Ready`、`Booting`、`Paused`），导致包裹创建被状态拦截机制拒绝。

**解决方案**：确保系统状态机转换到 `Running` 状态，而不仅仅是服务进程在运行。

**验证方法**：`GET /api/system/status` 返回 `"currentState": "Running"`

---

**文档版本**: 1.0  
**创建时间**: 2025-12-13  
**维护者**: ZakYip Development Team
