# Server模式双实例问题分析与修复

## 问题描述

**症状**：在 Server 模式下，`POST /api/communication/test-parcel` 能成功向上游发送信息，但包裹创建时无法向上游发送通知。

## 根本原因

系统在 Server 模式下创建了**两个独立的 `IRuleEngineServer` 实例**，导致它们各自维护独立的客户端连接列表：

### 实例 1：`UpstreamRoutingClientFactory` 创建的服务器

**创建位置**：
```csharp
// UpstreamRoutingClientFactory.CreateServerModeAdapter() - 第150-164行
private IUpstreamRoutingClient CreateServerModeAdapter(UpstreamConnectionOptions options)
{
    // 创建RuleEngine服务器实例
    var serverFactory = new RuleEngineServerFactory(_loggerFactory, _systemClock);
    var server = serverFactory.CreateServer(options);  // ⬅️ 实例 1
    
    // 使用适配器包装服务器
    return new ServerModeClientAdapter(server, ...);
}
```

**注册方式**：
```csharp
// CommunicationServiceExtensions.cs - 第140-144行
services.AddSingleton<IUpstreamRoutingClient>(sp =>
{
    var factory = sp.GetRequiredService<IUpstreamRoutingClientFactory>();
    return factory.CreateClient();  // 在 Server 模式下返回 ServerModeClientAdapter
});
```

**使用场景**：
- 注入到 `SortingOrchestrator` 作为 `IUpstreamRoutingClient`
- 包裹创建时调用 `_upstreamClient.SendAsync(new ParcelDetectedMessage { ... })`
- 实际调用 `ServerModeClientAdapter` → `server.BroadcastParcelDetectedAsync()`

### 实例 2：`UpstreamServerBackgroundService` 创建的服务器

**创建位置**：
```csharp
// UpstreamServerBackgroundService - 第74-79行
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // 创建并启动服务器
    await StartServerAsync(stoppingToken);  // ⬅️ 创建实例 2
}

private async Task StartServerAsync(CancellationToken cancellationToken)
{
    _currentServer = _serverFactory.CreateServer(_currentOptions);  // ⬅️ 实例 2
    await _currentServer.StartAsync(cancellationToken);
}
```

**注册方式**：
```csharp
// CommunicationServiceExtensions.cs - 第257-258行
services.AddSingleton<UpstreamServerBackgroundService>();
services.AddHostedService(sp => sp.GetRequiredService<UpstreamServerBackgroundService>());
```

**使用场景**：
- 注入到 `CommunicationController`
- `test-parcel` 端点调用 `_serverBackgroundService.CurrentServer.BroadcastChuteAssignmentAsync()`

## 问题分析

### 为什么 test-parcel 能发送？

`test-parcel` 端点使用 `UpstreamServerBackgroundService.CurrentServer`（实例 2），这个实例：
- ✅ 在后台服务启动时创建并启动（`ExecuteAsync`）
- ✅ 监听配置的端口（如 `0.0.0.0:8000`）
- ✅ 接受上游客户端连接
- ✅ 维护客户端连接列表（`ConnectedClientsCount > 0`）

### 为什么包裹创建不能发送？

包裹创建流程使用 `ServerModeClientAdapter` 包装的服务器（实例 1），这个实例：
- ❌ **从未启动**（没有调用 `StartAsync()`）
- ❌ **不监听任何端口**
- ❌ **没有客户端连接**（`ConnectedClientsCount = 0`）
- ❌ 调用 `BroadcastParcelDetectedAsync()` 时广播到空列表

### 架构图

```
包裹创建流程:
SortingOrchestrator
    ↓ 注入
IUpstreamRoutingClient (DI容器)
    ↓ 实际类型
ServerModeClientAdapter
    ↓ 包装
IRuleEngineServer (实例 1) ❌ 未启动，无客户端
    ↓ 调用
BroadcastParcelDetectedAsync() → 广播到 0 个客户端

test-parcel 端点:
CommunicationController
    ↓ 注入
UpstreamServerBackgroundService
    ↓ 属性
CurrentServer (实例 2) ✅ 已启动，有客户端
    ↓ 调用
BroadcastChuteAssignmentAsync() → 广播到 N 个客户端
```

## 修复方案

### 方案 1：统一使用 UpstreamServerBackgroundService（推荐）

**思路**：让 `ServerModeClientAdapter` 不创建新的服务器实例，而是引用 `UpstreamServerBackgroundService.CurrentServer`。

**实施步骤**：

1. 修改 `UpstreamRoutingClientFactory.CreateServerModeAdapter()`：
   ```csharp
   private IUpstreamRoutingClient CreateServerModeAdapter(
       UpstreamConnectionOptions options,
       UpstreamServerBackgroundService? serverBackgroundService)
   {
       // 不再创建新的服务器实例
       // 而是从 UpstreamServerBackgroundService 获取已启动的实例
       
       if (serverBackgroundService?.CurrentServer == null)
       {
           throw new InvalidOperationException(
               "Server 模式下 UpstreamServerBackgroundService.CurrentServer 为 null。" +
               "请确保 UpstreamServerBackgroundService 已启动。");
       }
       
       return new ServerModeClientAdapter(
           serverBackgroundService.CurrentServer,  // ⬅️ 使用已启动的实例
           _loggerFactory.CreateLogger<ServerModeClientAdapter>(),
           _systemClock);
   }
   ```

2. 修改工厂注册，注入 `UpstreamServerBackgroundService`：
   ```csharp
   services.AddSingleton<IUpstreamRoutingClientFactory>(sp =>
   {
       var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
       var systemClock = sp.GetRequiredService<ISystemClock>();
       var configRepository = sp.GetRequiredService<ICommunicationConfigurationRepository>();
       var serverBackgroundService = sp.GetService<UpstreamServerBackgroundService>();
       
       Func<UpstreamConnectionOptions> optionsProvider = () =>
       {
           var dbConfig = configRepository.Get();
           return MapFromDatabaseConfig(dbConfig);
       };
       
       return new UpstreamRoutingClientFactory(
           loggerFactory, 
           optionsProvider, 
           systemClock,
           serverBackgroundService);  // ⬅️ 传递后台服务
   });
   ```

3. 修改 `UpstreamRoutingClientFactory` 构造函数：
   ```csharp
   private readonly UpstreamServerBackgroundService? _serverBackgroundService;
   
   public UpstreamRoutingClientFactory(
       ILoggerFactory loggerFactory,
       Func<UpstreamConnectionOptions> optionsProvider,
       ISystemClock systemClock,
       UpstreamServerBackgroundService? serverBackgroundService = null)
   {
       _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
       _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
       _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
       _serverBackgroundService = serverBackgroundService;
   }
   ```

**优点**：
- ✅ 统一使用一个服务器实例
- ✅ 客户端连接列表一致
- ✅ 最小代码变更
- ✅ 保持现有架构

**缺点**：
- ⚠️ 需要处理启动顺序问题（`UpstreamServerBackgroundService` 必须先启动）
- ⚠️ 工厂依赖后台服务，增加耦合

### 方案 2：移除 UpstreamServerBackgroundService，统一使用 ServerModeClientAdapter

**思路**：移除独立的后台服务，让 `ServerModeClientAdapter` 在首次使用时启动服务器。

**实施步骤**：

1. 在 `ServerModeClientAdapter` 中添加延迟启动逻辑：
   ```csharp
   public async Task<bool> SendAsync(IUpstreamMessage message, CancellationToken cancellationToken = default)
   {
       // 确保服务器已启动
       if (!_server.IsRunning)
       {
           await ConnectAsync(cancellationToken);
       }
       
       // 发送消息
       // ...
   }
   ```

2. 移除 `UpstreamServerBackgroundService` 注册

3. 修改 `CommunicationController` 直接使用 `IUpstreamRoutingClient`

**优点**：
- ✅ 只有一个服务器实例
- ✅ 架构更简洁
- ✅ 无启动顺序问题

**缺点**：
- ❌ 需要修改 `CommunicationController` 的依赖
- ❌ 需要处理延迟启动的复杂性
- ❌ 影响范围较大

### 方案 3：在 ServerModeClientAdapter 中引用 UpstreamServerBackgroundService（最简单）

**思路**：让 `ServerModeClientAdapter` 直接持有 `UpstreamServerBackgroundService` 的引用，通过它访问服务器实例。

**实施步骤**：

1. 修改 `ServerModeClientAdapter` 构造函数：
   ```csharp
   public sealed class ServerModeClientAdapter : IUpstreamRoutingClient
   {
       private readonly UpstreamServerBackgroundService _serverBackgroundService;
       private readonly ILogger<ServerModeClientAdapter> _logger;
       private readonly ISystemClock _systemClock;
       
       public ServerModeClientAdapter(
           UpstreamServerBackgroundService serverBackgroundService,
           ILogger<ServerModeClientAdapter> logger,
           ISystemClock systemClock)
       {
           _serverBackgroundService = serverBackgroundService ?? 
               throw new ArgumentNullException(nameof(serverBackgroundService));
           _logger = logger ?? throw new ArgumentNullException(nameof(logger));
           _systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
       }
       
       private IRuleEngineServer Server => _serverBackgroundService.CurrentServer 
           ?? throw new InvalidOperationException("Server instance not available");
       
       public bool IsConnected => Server.IsRunning;
       
       public async Task<bool> SendAsync(IUpstreamMessage message, CancellationToken cancellationToken = default)
       {
           return message switch
           {
               ParcelDetectedMessage detected => await Server.BroadcastParcelDetectedAsync(detected.ParcelId, cancellationToken),
               // ...
           };
       }
   }
   ```

2. 修改 `UpstreamRoutingClientFactory.CreateServerModeAdapter()`：
   ```csharp
   private IUpstreamRoutingClient CreateServerModeAdapter(
       UpstreamConnectionOptions options,
       UpstreamServerBackgroundService serverBackgroundService)
   {
       return new ServerModeClientAdapter(
           serverBackgroundService,  // ⬅️ 传递后台服务
           _loggerFactory.CreateLogger<ServerModeClientAdapter>(),
           _systemClock);
   }
   ```

**优点**：
- ✅ 代码变更最小
- ✅ 统一使用一个服务器实例
- ✅ 不破坏现有架构

**缺点**：
- ⚠️ 需要处理 `CurrentServer` 为 null 的情况

## 修复方案

### ✅ 已实施：方案 3 - 统一使用 UpstreamServerBackgroundService

**实施内容**：

1. **修改 `ServerModeClientAdapter` 构造函数**：
   ```csharp
   public ServerModeClientAdapter(
       UpstreamServerBackgroundService serverBackgroundService,  // ⬅️ 引用后台服务
       ILogger<ServerModeClientAdapter> logger,
       ISystemClock systemClock)
   ```

2. **通过属性访问服务器实例**：
   ```csharp
   private IRuleEngineServer Server => _serverBackgroundService.CurrentServer 
       ?? throw new InvalidOperationException("服务器实例不可用");
   ```

3. **修改 `UpstreamRoutingClientFactory`**：
   - 添加 `UpstreamServerBackgroundService` 参数
   - 在 Server 模式下传递后台服务给适配器

4. **修改 DI 注册**：
   ```csharp
   services.AddSingleton<IUpstreamRoutingClientFactory>(sp =>
   {
       var serverBackgroundService = sp.GetService<UpstreamServerBackgroundService>();
       return new UpstreamRoutingClientFactory(
           loggerFactory, 
           optionsProvider, 
           systemClock, 
           serverBackgroundService);  // ⬅️ 传递后台服务
   });
   ```

**结果**：
- ✅ 整个系统只有**一个** `IRuleEngineServer` 实例
- ✅ 包裹创建和 test-parcel 使用**同一个**服务器实例
- ✅ 客户端连接列表**完全一致**
- ✅ `ConnectedClientsCount` 在所有地方都是相同的值

**验证方法**：

修复后，包裹创建时的日志应该显示正确的客户端数量：

```
[信息] [PR-42 Parcel-First] 发送上游包裹检测通知: ParcelId=xxx
[信息] [服务端模式-适配器] 转换NotifyParcelDetectedAsync为BroadcastParcelDetectedAsync: 
       ParcelId=xxx, ServerIsRunning=True, ConnectedClientsCount=2  ⬅️ 现在应该 > 0
[信息] [服务端模式-广播-成功] 已向客户端 xxx 广播包裹检测通知: ParcelId=xxx
```

---

## 推荐方案

~~**推荐方案 3**，理由：~~
~~1. 代码变更最小~~
~~2. 不破坏现有架构~~
~~3. 统一使用 `UpstreamServerBackgroundService.CurrentServer`~~
~~4. 所有使用方（`SortingOrchestrator` 和 `CommunicationController`）都通过同一个服务器实例访问客户端连接~~

**✅ 已实施完成（PR-DUAL-INSTANCE-FIX）**

---

**文档版本**: 1.0  
**创建时间**: 2025-12-13  
**维护者**: ZakYip Development Team
