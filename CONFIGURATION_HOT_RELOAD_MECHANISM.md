# 配置热更新机制说明

## 概述

系统支持**配置热更新**，当通过 API 更新系统配置时，配置变更会**立即生效**，无需重启服务。

## 热更新流程

### 1. API 更新触发

当调用 `PUT /api/config/system` 更新系统配置时：

```http
PUT /api/config/system
Content-Type: application/json

{
  "enableTimeoutDetection": false,
  "enableInterferenceDetection": true,
  "passThroughOnInterference": false,
  "exceptionChuteId": 999
}
```

### 2. 配置持久化 + 缓存刷新

`SystemConfigService.UpdateSystemConfigAsync` 方法执行以下步骤：

```csharp
// 1. 写入数据库
_repository.Update(config);

// 2. 立即刷新缓存
var updatedConfig = _repository.Get();
_configCache.Set(SystemConfigCacheKey, updatedConfig);

// 3. 记录审计日志
_auditLogger.LogConfigurationChange(...);
```

**关键点**：
- ✅ 缓存刷新**立即发生**，在数据库写入后的下一行代码
- ✅ 使用 `IMemoryCache` 实现，线程安全
- ✅ 无延迟，无异步等待

### 3. 配置读取

业务代码（如 `SortingOrchestrator`）通过 `ISystemConfigService.GetSystemConfig()` 获取配置：

```csharp
var systemConfig = _systemConfigService.GetSystemConfig();
var enableTimeoutDetection = systemConfig?.EnableTimeoutDetection ?? false;
```

**配置使用点**：

#### `EnableTimeoutDetection` (超时和丢包检测开关)

- **文件**: `src/Execution/.../Orchestration/SortingOrchestrator.cs`
- **行号**: 1247-1252
- **检查逻辑**:
  ```csharp
  var enableTimeoutDetection = systemConfig?.EnableTimeoutDetection ?? false;
  
  if (enableTimeoutDetection && currentTime > task.ExpectedArrivalTime)
  {
      // 超时/丢包检测逻辑
  }
  ```

#### `EnableEarlyTriggerDetection` (干扰检测开关)

- **文件**: `src/Execution/.../Orchestration/SortingOrchestrator.cs`
- **行号**: 1182-1211
- **检查逻辑**:
  ```csharp
  var enableEarlyTriggerDetection = systemConfig?.EnableEarlyTriggerDetection ?? false;
  
  if (enableEarlyTriggerDetection && currentTime < earliestDequeueTime)
  {
      // 提前触发检测逻辑
  }
  ```

## 配置默认值

| 配置项 | API 字段名 | 默认值 | 说明 |
|--------|-----------|--------|------|
| `EnableTimeoutDetection` | `enableTimeoutDetection` | **false** | 超时检测和包裹丢失检测 |
| `EnableEarlyTriggerDetection` | `enableInterferenceDetection` | **true** | 干扰检测（提前触发检测） |
| `PassThroughOnInterference` | `passThroughOnInterference` | false | 干扰时直行动作 |
| `ExceptionChuteId` | `exceptionChuteId` | 999 | 异常格口 ID |

**注意**：API 中 `enableInterferenceDetection` 对应代码中的 `EnableEarlyTriggerDetection`。

## 验证配置生效

### 方法 1: 通过 API 查询

```bash
# 更新配置
curl -X PUT http://localhost:5000/api/config/system \
  -H "Content-Type: application/json" \
  -d '{"enableTimeoutDetection": false, "enableInterferenceDetection": true}'

# 验证配置
curl http://localhost:5000/api/config/system
```

**期望输出**:
```json
{
  "success": true,
  "data": {
    "enableTimeoutDetection": false,
    "enableInterferenceDetection": true,
    ...
  }
}
```

### 方法 2: 检查日志

更新配置后，查看日志中是否有配置更新记录：

```log
2025-12-28 09:00:00.123|INFO|SystemConfigService|系统配置已更新（热更新生效）: ExceptionChuteId=999, Version=2
```

禁用 `EnableTimeoutDetection` 后，日志中**不应再出现**：

```log
[超时检测] 包裹 xxx 在 Position x 超时
[包裹丢失] 包裹 xxx 在 Position x 判定为丢失
[包裹丢失清理] Position x 移除了包裹 xxx 的 x 个任务
```

## 常见问题排查

### Q1: 更新配置后仍出现超时/丢包日志

**可能原因**：

1. **API 更新失败** - 检查 HTTP 响应状态码是否为 200
2. **配置字段名错误** - 确保使用 `enableTimeoutDetection` 而非 `EnableTimeoutDetection`
3. **多实例部署** - 确保所有实例都接收到配置更新
4. **缓存未刷新（极少见）** - 重启服务强制重新加载

**排查步骤**：

```bash
# 1. 检查 API 更新响应
curl -v -X PUT http://localhost:5000/api/config/system \
  -H "Content-Type: application/json" \
  -d '{"enableTimeoutDetection": false}'

# 2. 验证配置值
curl http://localhost:5000/api/config/system | jq '.data.enableTimeoutDetection'

# 3. 检查日志中的配置读取
grep "EnableTimeoutDetection" /var/log/sorting/*.log
```

### Q2: 禁用后队列任务被删除

**原因**：`EnableTimeoutDetection` 控制的是**检测逻辑是否执行**，而非队列操作本身。

**正确行为**：

- `EnableTimeoutDetection = false` → **不触发**超时/丢包检测逻辑 → **不删除**队列任务
- `EnableTimeoutDetection = true` → **触发**超时/丢包检测逻辑 → **可能删除**队列任务（仅在判定为丢失时）

**验证**：

```bash
# 禁用检测
curl -X PUT http://localhost:5000/api/config/system \
  -d '{"enableTimeoutDetection": false}'

# 观察日志 - 应无 "[包裹丢失清理]" 日志
tail -f /var/log/sorting/*.log | grep "包裹丢失清理"
```

### Q3: API 字段名与代码不一致

这是**设计行为**，API 使用更直观的命名：

| 代码字段 | API 字段 | 原因 |
|---------|---------|------|
| `EnableEarlyTriggerDetection` | `enableInterferenceDetection` | "干扰检测" 比 "提前触发检测" 更易理解 |
| `EnableTimeoutDetection` | `enableTimeoutDetection` | 一致 |

**映射代码**: `src/Host/.../Controllers/ConfigurationController.cs`

```csharp
public record SystemConfigUpdateRequest
{
    [JsonPropertyName("enableTimeoutDetection")]
    public bool? EnableTimeoutDetection { get; init; }
    
    [JsonPropertyName("enableInterferenceDetection")]
    public bool? EnableEarlyTriggerDetection { get; init; }
}
```

## 性能影响

- **配置读取开销**: < 1μs（内存缓存）
- **配置更新开销**: ~5-10ms（数据库写入 + 缓存刷新）
- **生效延迟**: 0ms（下一次配置读取立即使用新值）

## 相关文档

- **`PACKET_LOSS_DETECTION_ISSUE.md`** - 包裹丢失检测问题排查
- **`POSITION_INTERVAL_PERFORMANCE_ANALYSIS.md`** - Position 0 → 1 性能问题分析
- **`docs/guides/SYSTEM_CONFIG_GUIDE.md`** - 系统配置完整说明
