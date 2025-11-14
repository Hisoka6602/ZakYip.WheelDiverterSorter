# EMC 分布式锁重构

## 概述

本次重构将雷赛（Leadshine）EMC 分布式锁实现与 [ZakYip.Singulation](https://github.com/Hisoka6602/ZakYip.Singulation) 项目的设计保持一致。

## 变更内容

### 新增接口：`IEmcResourceLock`

位置：`ZakYip.WheelDiverterSorter.Drivers/Leadshine/IEmcResourceLock.cs`

简单、专注的分布式锁接口，定义了基本的锁操作：

```csharp
public interface IEmcResourceLock : IDisposable
{
    Task<bool> TryAcquireAsync(TimeSpan timeout, CancellationToken ct = default);
    void Release();
    string LockIdentifier { get; }
    bool IsLockHeld { get; }
}
```

### 新增实现：`EmcNamedMutexLock`

位置：`ZakYip.WheelDiverterSorter.Drivers/Leadshine/EmcNamedMutexLock.cs`

基于 Windows 命名互斥锁的实现：

**特性：**
- 使用全局命名互斥锁：`Global\\ZakYip_EMC_{resourceName}`
- 跨进程同步（同一台机器）
- 自动处理放弃的互斥锁
- 完整的日志记录
- 正确的资源释放模式

**使用示例：**

```csharp
var logger = loggerFactory.CreateLogger<EmcNamedMutexLock>();
using var resourceLock = new EmcNamedMutexLock(logger, "CardNo_0");

var acquired = await resourceLock.TryAcquireAsync(TimeSpan.FromSeconds(30));
if (acquired)
{
    try
    {
        // 执行需要独占访问的操作
        await PerformResetAsync();
    }
    finally
    {
        resourceLock.Release();
    }
}
```

### 增强的 `CoordinatedEmcController`

位置：`ZakYip.WheelDiverterSorter.Drivers/Leadshine/CoordinatedEmcController.cs`

**新增功能：**
- 支持三种模式的构造函数：
  1. 无锁模式（单实例）
  2. TCP 锁模式（现有实现，用于分布式系统）
  3. 命名互斥锁模式（新实现，推荐用于单机多进程）
- 自动锁类型检测
- 向后兼容现有代码

**使用示例：**

```csharp
// 使用命名互斥锁（推荐）
var resourceLock = new EmcNamedMutexLock(lockLogger, $"CardNo_{cardNo}");
var controller = new CoordinatedEmcController(
    logger,
    emcController,
    resourceLock
);

// 使用 TCP 锁（现有方式，仍然支持）
var controller = new CoordinatedEmcController(
    logger,
    emcController,
    lockManager
);
```

## 测试

位置：`ZakYip.WheelDiverterSorter.Drivers.Tests/Leadshine/EmcNamedMutexLockTests.cs`

**测试覆盖：**
- 构造函数验证
- 锁获取（首次、重复获取）
- 锁释放
- 销毁行为
- 错误处理
- 边界情况

**结果：** ✅ 9/9 测试通过

## 使用示例

位置：`ZakYip.WheelDiverterSorter.Drivers/Leadshine/EmcDistributedLockUsageExample.cs`

包含完整的示例代码：
1. 基本的命名互斥锁使用
2. 与 CoordinatedEmcController 集成
3. 多实例协调场景说明
4. 处理锁获取失败

## 对比：命名互斥锁 vs TCP 锁

| 特性 | 命名互斥锁（新） | TCP 锁（现有） |
|------|----------------|---------------|
| 配置复杂度 | 非常简单 | 需要锁服务器 |
| 跨进程 | ✅ 仅同一台机器 | ✅ 跨机器 |
| 操作系统管理 | ✅ 是 | ❌ 否 |
| 事件通知 | ❌ 无 | ✅ 有 |
| 放弃锁处理 | ✅ 自动 | ❌ 手动 |
| 适用场景 | 单机部署 | 分布式系统 |

## 使用建议

**使用命名互斥锁的场景：**
- 在单台机器上运行多个进程
- 需要零配置解决方案
- 不需要跨机器协调
- 需要操作系统级别的可靠性

**使用 TCP 锁的场景：**
- 跨多台机器运行
- 需要实例间的事件通知
- 需要集中化的锁管理
- 需要自定义协调逻辑

## 向后兼容性

所有使用 `IEmcResourceLockManager`（TCP 锁）的现有代码无需修改即可继续工作。新的命名互斥锁实现是纯新增功能。

## 迁移路径

从 TCP 锁切换到命名互斥锁：

**之前：**
```csharp
var controller = new CoordinatedEmcController(
    logger,
    emcController,
    lockManager  // IEmcResourceLockManager
);
```

**之后：**
```csharp
var resourceLock = new EmcNamedMutexLock(lockLogger, $"CardNo_{cardNo}");
var controller = new CoordinatedEmcController(
    logger,
    emcController,
    resourceLock  // IEmcResourceLock
);
```

## 安全性

- ✅ **CodeQL 分析**：未检测到漏洞
- ✅ **正确的资源释放**：正确实现 IDisposable
- ✅ **无代码中的密钥**：所有配置都是外部的
- ✅ **输入验证**：验证资源名称

## 构建状态

✅ **构建**：成功  
✅ **测试**：9/9 通过  
✅ **安全**：无漏洞  

## 参考项目

本次重构参考了 [ZakYip.Singulation](https://github.com/Hisoka6602/ZakYip.Singulation) 项目的雷赛分布式锁设计和实现。
