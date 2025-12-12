# TD-UPSTREAM-REFACTOR: 上游接口彻底重构技术债

## 状态
❌ **未开始** - 必须在当前PR完成，否则PR失败

## 问题描述
当前上游接口保留了旧方法（ConnectAsync、DisconnectAsync、NotifyParcelDetectedAsync、NotifySortingCompletedAsync），
违反了"彻底重构，不保留旧代码"的要求。

## 目标
删除所有4个旧方法，只保留统一的1事件+2方法接口：
- 1个事件：`ChuteAssigned`
- 2个方法：`SendAsync(IUpstreamMessage)`、`PingAsync()`
- 1个扩展：`UpdateOptionsAsync()`

## 已完成
- ✅ 移动`UpstreamMessageType`枚举到`Core/Enums/Communication/`
- ✅ 在接口定义中添加新方法
- ✅ 创建消息类型（ParcelDetectedMessage、SortingCompletedMessage）

## 待完成步骤

### 步骤1: 重构RuleEngineClientBase (30分钟)
**文件**: `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/RuleEngineClientBase.cs`

**删除**:
```csharp
public abstract Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
public abstract Task DisconnectAsync();
public abstract Task<bool> NotifyParcelDetectedAsync(long parcelId, CancellationToken cancellationToken = default);
public abstract Task<bool> NotifySortingCompletedAsync(SortingCompletedNotification notification, CancellationToken cancellationToken = default);
```

**保留**:
```csharp
public abstract Task<bool> SendAsync(IUpstreamMessage message, CancellationToken cancellationToken = default);
public virtual Task<bool> PingAsync(CancellationToken cancellationToken = default);
public virtual Task UpdateOptionsAsync(UpstreamConnectionOptions options);
```

### 步骤2: 重构TcpRuleEngineClient (45分钟)
**文件**: `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/TcpRuleEngineClient.cs`

**修改**:
1. 将`ConnectAsync`改为`private`（内部连接管理）
2. 将`DisconnectAsync`改为`private`
3. 将`NotifyParcelDetectedAsync`改为`private SendParcelDetectedMessageAsync`
4. 将`NotifySortingCompletedAsync`改为`private SendSortingCompletedMessageAsync`
5. 添加`public override Task<bool> SendAsync(IUpstreamMessage message, CancellationToken cancellationToken)`

**SendAsync实现**:
```csharp
public override async Task<bool> SendAsync(IUpstreamMessage message, CancellationToken cancellationToken = default)
{
    ThrowIfDisposed();
    
    // 自动连接逻辑
    if (!IsConnected)
    {
        await ConnectAsync(cancellationToken);
    }
    
    return message switch
    {
        ParcelDetectedMessage detected => await SendParcelDetectedMessageAsync(detected.ParcelId, cancellationToken),
        SortingCompletedMessage completed => await SendSortingCompletedMessageAsync(completed.Notification, cancellationToken),
        _ => throw new ArgumentException($"不支持的消息类型: {message.GetType().Name}", nameof(message))
    };
}
```

### 步骤3: 重构SignalRRuleEngineClient (30分钟)
**文件**: `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/SignalRRuleEngineClient.cs`

同TcpRuleEngineClient的改法。

### 步骤4: 重构MqttRuleEngineClient (30分钟)
**文件**: `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/MqttRuleEngineClient.cs`

同TcpRuleEngineClient的改法。

### 步骤5: 重构TouchSocketTcpRuleEngineClient (30分钟)
**文件**: `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/TouchSocketTcpRuleEngineClient.cs`

同TcpRuleEngineClient的改法。

### 步骤6: 重构ServerModeClientAdapter (20分钟)
**文件**: `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Adapters/ServerModeClientAdapter.cs`

**删除**:
- `ConnectAsync`
- `DisconnectAsync`
- `NotifyParcelDetectedAsync`
- `NotifySortingCompletedAsync`

**保留**:
- `SendAsync` (已实现)
- `PingAsync` (已实现)
- `UpdateOptionsAsync` (已实现)

### 步骤7: 重构SimulatedUpstreamRoutingClient (20分钟)
**文件**: `src/Simulation/ZakYip.WheelDiverterSorter.Simulation.Cli/Clients/SimulatedUpstreamRoutingClient.cs`

同ServerModeClientAdapter的改法。

### 步骤8: 更新SortingOrchestrator (60分钟)
**文件**: `src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs`

**查找替换**:
```csharp
// 旧代码
await _upstreamClient.ConnectAsync(cancellationToken);
await _upstreamClient.DisconnectAsync();
await _upstreamClient.NotifyParcelDetectedAsync(parcelId, cancellationToken);
await _upstreamClient.NotifySortingCompletedAsync(notification, cancellationToken);

// 新代码
// 删除ConnectAsync和DisconnectAsync调用（连接自动管理）
await _upstreamClient.SendAsync(new ParcelDetectedMessage 
{ 
    ParcelId = parcelId, 
    DetectedAt = new DateTimeOffset(_clock.LocalNow) 
}, cancellationToken);

await _upstreamClient.SendAsync(new SortingCompletedMessage 
{ 
    Notification = notification 
}, cancellationToken);
```

### 步骤9: 更新CommunicationController (15分钟)
**文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/Controllers/CommunicationController.cs`

删除所有手动调用ConnectAsync/DisconnectAsync的地方。

### 步骤10: 更新SimulationRunner (15分钟)
**文件**: `src/Simulation/ZakYip.WheelDiverterSorter.Simulation/Services/SimulationRunner.cs`

同步骤9。

### 步骤11: 更新SortingServicesInitHostedService (15分钟)
**文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/Services/Workers/SortingServicesInitHostedService.cs`

同步骤9。

### 步骤12: 更新CommunicationConfigService (15分钟)
**文件**: `src/Application/ZakYip.WheelDiverterSorter.Application/Services/Config/CommunicationConfigService.cs`

同步骤9。

### 步骤13: 验证编译 (10分钟)
```bash
dotnet build
dotnet test --no-build
```

## 预计工作量
总计：**6-8小时**（涉及13个步骤，12+个文件）

## 风险
- 🔴 **高风险**：影响所有上游通信链路
- ⚠️ 需要完整的集成测试验证
- ⚠️ Client和Server两种模式都需要验证

## 实施建议
由于工作量大且风险高，建议：
1. 分2个PR实施（步骤1-7一个PR，步骤8-13一个PR）
2. 每步完成后立即编译验证
3. 完成后运行完整的E2E测试

## 验收标准
- ✅ 所有旧方法（ConnectAsync、DisconnectAsync、NotifyParcelDetectedAsync、NotifySortingCompletedAsync）已删除
- ✅ 所有Client实现类已实现SendAsync
- ✅ 所有调用方已更新为使用SendAsync
- ✅ Client和Server两种模式都能正常工作
- ✅ 编译0 errors, 0 warnings
- ✅ 所有测试通过
