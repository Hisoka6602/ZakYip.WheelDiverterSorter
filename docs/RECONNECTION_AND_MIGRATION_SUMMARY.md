# 摆轮无限重连机制与数据库迁移实施总结

## 概述

本次PR实现了两个关键功能：
1. **摆轮设备断开后的无限重连机制**
2. **ConveyorSegment数据库ID类型迁移工具**

## 一、摆轮无限重连机制

### 问题描述

在运行中摆轮设备故障/断开后，系统再也不会重连那个摆轮了。驱动本身只会在 `CheckHeartbeatAsync` 里尝试重连：如果发现流/接收任务/TCP 状态异常，或心跳超时，才调用 `ReconnectAsync`；每次调用只重连一次，不会自带循环。

触发 `CheckHeartbeatAsync` 的唯一入口是后台心跳监控任务；它以 3 秒为周期轮询驱动器，一次心跳周期只触发一次重连机会。如果该后台任务因停止、异常或驱动器未在活动列表中而未运行，就不会再触发重连。

### 解决方案

#### 1. 在 `ShuDiNiaoWheelDiverterDriver` 中添加重连机制

**新增字段**：
```csharp
// 重连相关字段
private Task? _reconnectTask;
private CancellationTokenSource? _reconnectCts;

// 重连常量
private const int MaxReconnectBackoffMs = 2000;  // 最大退避 2 秒
private const int InitialReconnectBackoffMs = 200;  // 初始退避 200 毫秒
```

**新增方法**：

1. **`ReconnectAsync()`** - 公共方法，启动重连任务
   - 检查是否已有重连任务在运行，避免重复启动
   - 创建新的后台任务执行无限重连

2. **`ReconnectLoopAsync(CancellationToken)`** - 私有方法，实现无限重连循环
   - 使用指数退避策略（200ms → 400ms → 800ms → 1600ms → 2000ms 上限）
   - 调用 `EnsureConnectedAsync` 尝试连接
   - 连接成功后自动退出循环
   - 连接失败后等待退避时间再重试
   - 捕获所有异常，确保循环持续运行
   - 遵守 `copilot-instructions.md` 第三章第2条：退避时间上限为 2 秒

3. **`StopReconnectTask()`** - 私有方法，停止重连任务
   - 取消重连任务的 CancellationToken
   - 等待任务完成（最多 2 秒）
   - 在 `Dispose()` 方法中调用

#### 2. 在 `WheelDiverterHeartbeatMonitor` 中触发重连

**修改位置**：心跳检查失败且超时的异常处理分支

```csharp
// 心跳超时，标记为不健康
if (_lastHealthStatus.TryGetValue(diverterId, out var wasPreviouslyHealthy) && wasPreviouslyHealthy)
{
    _logger.LogError(
        "摆轮 {DiverterId} 心跳超时！最后成功时间: {LastSuccess}, 已超时: {Elapsed}",
        diverterId, lastSuccess, elapsed);
}

_lastHealthStatus[diverterId] = false;

// 更新健康状态为不健康
UpdateWheelDiverterHealth(diverterId, false, 
    $"心跳超时: {elapsed.TotalSeconds:F1}秒", "连接异常");

// 触发重连（数递鸟驱动器支持 ReconnectAsync）
if (driver is Drivers.Vendors.ShuDiNiao.ShuDiNiaoWheelDiverterDriver shuDiNiaoDriver)
{
    _logger.LogInformation("摆轮 {DiverterId} 心跳超时，触发自动重连", diverterId);
    shuDiNiaoDriver.ReconnectAsync();
}
```

### 测试验证

创建了 `ShuDiNiaoReconnectionTests.cs`，包含 5 个测试用例：

1. **`ReconnectAsync_ShouldNotThrow_WhenCalledMultipleTimes`** - 验证多次调用重连方法不会抛出异常
2. **`CheckHeartbeatAsync_ShouldReturnFalse_WhenNeverConnected`** - 验证未连接时心跳检查返回 false
3. **`Dispose_ShouldStopReconnectTask`** - 验证释放资源时会停止重连任务
4. **`TurnLeftAsync_ShouldAttemptConnection_WhenNotConnected`** - 验证发送命令时会尝试连接
5. **`GetStatusAsync_ShouldReturnDisconnected_WhenNotConnected`** - 验证未连接时状态为"未连接"

**测试结果**：✅ 全部通过（5/5）

---

## 二、ConveyorSegment 数据库 ID 类型迁移

### 问题描述

用户报告错误：
```json
{
  "success": false,
  "code": "ServerError",
  "message": "获取输送线段配置列表失败: Unable to cast object of type 'LiteDB.ObjectId' to type 'System.Int64'.",
  "data": null,
  "timestamp": "2025-12-24T23:15:03.0043323+08:00"
}
```

**根本原因**：
- 旧数据库中 `ConveyorSegmentConfiguration` 集合的 `_id` 字段为 `ObjectId` 类型
- 新代码配置 `SegmentId`（`long` 类型）作为主键
- LiteDB 尝试将 `ObjectId` 转换为 `Int64` 时失败

> **⚠️ 废弃说明 (2025-12-24)**：
> 
> 以下描述的 ObjectId → Int64 迁移方案已废弃，不再使用。
> 
> **实际采用方案**：保留数据库中的 ObjectId 格式 `_id`，在代码中忽略 `Id` 字段映射，使用 `SegmentId` 作为业务主键。这种方案无需数据迁移，向后兼容所有现有数据。
> 
> 详见 TD-083 和相关 PR: "Fix ConveyorSegmentConfiguration ObjectId compatibility"

### 解决方案（已废弃，仅供参考）

创建自动迁移工具 `ConveyorSegmentIdMigration`，在仓储初始化时自动执行。

#### 迁移流程

1. **检测是否需要迁移**
   - 检查集合是否存在
   - 检查集合是否为空
   - 读取第一条记录，检查 `_id` 字段类型
   - 如果 `_id` 已是 `Int64`，无需迁移
   - 如果 `_id` 是 `ObjectId`，执行迁移

2. **备份原数据**
   - 删除旧备份集合（如果存在）
   - 将原集合重命名为 `ConveyorSegmentConfiguration_Backup`

3. **迁移数据**
   - 读取所有旧数据（使用 `BsonDocument`，不依赖类型映射）
   - 创建新集合（使用正确的映射配置）
   - 逐条迁移：
     - 提取 `SegmentId` 字段值
     - 创建新文档，将 `_id` 设置为 `SegmentId`
     - 插入到新集合
   - 记录成功/失败数量

4. **清理**
   - 如果全部迁移成功，删除备份集合
   - 如果部分失败，保留备份集合

#### 代码实现

**迁移工具类**：`ConveyorSegmentIdMigration.cs`
```csharp
public static (bool MigrationNeeded, bool Success, string Message) Migrate(
    string databasePath,
    ILogger? logger = null)
```

**仓储自动迁移**：`LiteDbConveyorSegmentRepository.cs`
```csharp
public LiteDbConveyorSegmentRepository(
    string databasePath, 
    ISystemClock systemClock,
    ILogger<LiteDbConveyorSegmentRepository>? logger = null)
{
    // 在打开数据库前执行迁移（如果需要）
    try
    {
        var (migrationNeeded, success, message) = 
            ConveyorSegmentIdMigration.Migrate(databasePath, logger);
        if (migrationNeeded)
        {
            if (success)
            {
                _logger?.LogInformation("ConveyorSegmentConfiguration 集合迁移成功: {Message}", message);
            }
            else
            {
                _logger?.LogWarning("ConveyorSegmentConfiguration 集合迁移失败: {Message}", message);
            }
        }
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "执行 ConveyorSegmentConfiguration 迁移时发生异常");
    }
    
    // 继续初始化数据库...
}
```

**DI 注册更新**：`WheelDiverterSorterServiceCollectionExtensions.cs`
```csharp
// 注册输送线段配置仓储为单例
services.AddSingleton<IConveyorSegmentRepository>(serviceProvider =>
{
    var clock = serviceProvider.GetRequiredService<ISystemClock>();
    var logger = serviceProvider.GetRequiredService<ILogger<LiteDbConveyorSegmentRepository>>();
    var repository = new LiteDbConveyorSegmentRepository(fullDatabasePath, clock, logger);
    return repository;
});
```

### 迁移特性

✅ **自动检测** - 检查 `_id` 字段类型，只在需要时执行迁移  
✅ **安全备份** - 迁移前备份原数据到独立集合  
✅ **逐条迁移** - 单条失败不影响其他记录  
✅ **详细日志** - 完整记录迁移过程和结果  
✅ **失败保护** - 迁移失败时保留备份数据  
✅ **透明执行** - 首次访问仓储时自动执行，用户无感知  

---

## 三、遵守规范

### 1. copilot-instructions.md 规范

✅ **第三章第2条 - 连接重试规则**：
- 连接失败必须进行**无限重试**
- 退避时间上限为 **2 秒**（硬编码）
- 更新连接参数后，使用新参数继续无限重试

✅ **第三章第3条 - 发送失败处理**：
- 发送失败**只记录日志**，不进行发送重试
- 由调用方决定如何处理（如路由到异常格口）

### 2. 新要求 - 数据库迁移

✅ **所有修改数据库的操作都需要有迁移工具**：
- 创建了 `ConveyorSegmentIdMigration` 迁移工具
- 不要求用户删除数据库重建
- 自动在首次访问时执行迁移
- 迁移过程有完整日志
- 迁移失败时保留数据安全

---

## 四、部署说明

### 1. 摆轮重连机制

**自动生效**：
- 系统启动后，心跳监控服务会自动运行
- 当检测到摆轮心跳超时时，自动触发重连
- 无需任何配置或手动操作

**日志输出**：
```
[Information] 摆轮 1 心跳超时，触发自动重连
[Information] 摆轮 1 开始重连循环，初始退避=200ms，最大退避=2000ms
[Information] 摆轮 1 尝试重连到 192.168.1.100:9000（当前退避=200ms）
[Warning] 摆轮 1 重连失败，400ms 后重试
[Information] 摆轮 1 尝试重连到 192.168.1.100:9000（当前退避=400ms）
[Information] 摆轮 1 连接成功，接收任务已启动
[Information] 摆轮 1 重连成功，退出重连循环
```

### 2. 数据库迁移

**自动执行**：
- 首次启动系统时，仓储初始化会自动检测并执行迁移
- 如果数据库已是新格式，跳过迁移
- 迁移过程对用户透明

**日志输出**：
```
[Information] 开始迁移集合 ConveyorSegmentConfiguration，共 15 条记录
[Information] 重命名 ConveyorSegmentConfiguration 为 ConveyorSegmentConfiguration_Backup
[Information] 从备份中读取了 15 条记录
[Information] 迁移完成：成功 15 条，失败 0 条
[Information] 迁移完全成功，删除备份集合 ConveyorSegmentConfiguration_Backup
[Information] ConveyorSegmentConfiguration 集合迁移成功: 迁移完成：成功 15 条，失败 0 条
```

**回滚方案**（如果需要）：
如果迁移后出现问题，可以手动回滚：
1. 停止系统
2. 使用 LiteDB 工具打开数据库
3. 删除 `ConveyorSegmentConfiguration` 集合
4. 将 `ConveyorSegmentConfiguration_Backup` 重命名为 `ConveyorSegmentConfiguration`
5. 重启系统

---

## 五、文件清单

### 新增文件
1. `src/Infrastructure/ZakYip.WheelDiverterSorter.Configuration.Persistence/Migrations/ConveyorSegmentIdMigration.cs` - 数据库迁移工具
2. `tests/ZakYip.WheelDiverterSorter.Drivers.Tests/Vendors/ShuDiNiao/ShuDiNiaoReconnectionTests.cs` - 重连机制测试

### 修改文件
1. `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/ShuDiNiao/ShuDiNiaoWheelDiverterDriver.cs` - 添加无限重连机制
2. `src/Host/ZakYip.WheelDiverterSorter.Host/Services/Workers/WheelDiverterHeartbeatMonitor.cs` - 心跳超时时触发重连
3. `src/Infrastructure/ZakYip.WheelDiverterSorter.Configuration.Persistence/Repositories/LiteDb/LiteDbConveyorSegmentRepository.cs` - 构造函数中执行迁移
4. `src/Application/ZakYip.WheelDiverterSorter.Application/Extensions/WheelDiverterSorterServiceCollectionExtensions.cs` - 注入日志记录器

---

## 六、测试结果

### 单元测试
✅ ShuDiNiao 驱动测试：66 个测试全部通过  
✅ 重连机制测试：5 个测试全部通过  

### 编译验证
✅ Configuration.Persistence 项目编译成功  
✅ Drivers 项目编译成功  
✅ Application 项目编译成功  
✅ Host 项目编译成功  

---

## 七、总结

本次 PR 成功实现了：

1. **无限重连机制**：解决了摆轮设备断开后无法自动重连的问题
   - 遵守退避策略规范（最大 2 秒）
   - 无限重试直到连接成功
   - 自动由心跳监控触发

2. **数据库迁移工具**：解决了 ObjectId → Int64 类型转换错误
   - 自动检测并执行迁移
   - 安全备份原数据
   - 详细日志记录
   - 失败时保护数据

3. **遵守所有规范**：
   - copilot-instructions.md 中的连接重试规则
   - 新要求中的数据库迁移规则
   - 代码质量和测试要求

用户现在可以：
- ✅ 放心使用现有数据库，无需删除重建
- ✅ 系统会自动修复数据类型问题
- ✅ 摆轮断开后会自动无限重连

---

**作者**：GitHub Copilot  
**日期**：2025-12-24  
**PR**: copilot/improve-reconnect-logic
