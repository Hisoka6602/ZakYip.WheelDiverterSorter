# 上游接口统一实施计划

## 目标

统一上游接口为 **1事件+2方法**：
- 1个事件：`ChuteAssigned` - 接收上游格口分配
- 2个方法：
  - `SendAsync(IUpstreamMessage)` - 统一发送接口
  - `PingAsync()` - 健康检查

## 实施步骤

### 步骤1: 添加新的消息类型（向后兼容）

在`IUpstreamRoutingClient.cs`中添加：
- `IUpstreamMessage` 接口
- `ParcelDetectedMessage` 记录
- `SortingCompletedMessage` 记录

### 步骤2: 在接口中添加新方法（保持旧方法）

添加新方法but不删除旧方法：
- `Task<bool> SendAsync(IUpstreamMessage, CancellationToken)`
- `Task<bool> PingAsync(CancellationToken)`
- `Task UpdateOptionsAsync(UpstreamConnectionOptions)`

### 步骤3: 在基类中实现新方法

在`RuleEngineClientBase`中实现新方法，内部调用现有方法。

### 步骤4: 更新所有调用方

将所有`NotifyParcelDetectedAsync`和`NotifySortingCompletedAsync`调用改为`SendAsync`。

### 步骤5: 删除旧方法

从接口和实现中删除：
- `ConnectAsync`
- `DisconnectAsync`  
- `NotifyParcelDetectedAsync`
- `NotifySortingCompletedAsync`

### 步骤6: 删除冗余接口和适配器

- 删除 `IUpstreamConnectionManager.cs`
- 删除 `IUpstreamSortingGateway.cs`
- 删除 `ServerModeClientAdapter.cs`
- 删除相关工厂类

### 步骤7: 更新DI注册

### 步骤8: 测试验证

## 当前进度

- [ ] 步骤1
- [ ] 步骤2
- [ ] 步骤3
- [ ] 步骤4
- [ ] 步骤5
- [ ] 步骤6
- [ ] 步骤7
- [ ] 步骤8
