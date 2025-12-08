# 修复雷赛连接和数递鸟心跳检查问题

**日期**: 2025-12-08  
**PR**: copilot/check-emc-controller-initialized  
**问题来源**: 用户反馈生产环境问题

---

## 问题描述

### 问题1: 雷赛 IO 操作返回错误码 9

**现象**:
- 调用 `LTDMC.dmc_write_outbit` 返回错误码 9（表示控制卡未初始化）
- 但雷赛官方示例代码能正常工作
- `IsAvailable()` 检查通过，但实际 IO 操作失败

**影响**:
- IO 联动功能无法正常工作
- 包裹无法正确路由到目标格口

### 问题2: 数递鸟摆轮心跳检查误报连接断开

**现象**:
- 日志显示"摆轮 1 心跳检查: TCP连接断开"
- 但实际上数递鸟摆轮连接正常，能正常返回状态数据 "51 52 57 51 51 50 FE"
- 设备每秒发送状态上报，但心跳检查仍然失败

**影响**:
- 误判设备离线，可能触发不必要的重连或告警
- 影响系统稳定性和可靠性

---

## 根本原因分析

### 问题1: 雷赛连接问题

**根本原因**:  
在 `LeadshineEmcController.InitializeAsync` 方法中，当检测到总线异常时会执行软复位，但**软复位后未重新调用初始化函数建立连接**。

**详细流程**:
1. 初始调用 `dmc_board_init_eth(_cardNo, _controllerIp)` 成功，返回 0
2. 检查总线状态，发现 `nmc_get_errcode` 返回非 0 错误码
3. 调用 `dmc_soft_reset(_cardNo)` 执行软复位
4. 等待 500ms
5. **❌ 缺失步骤**: 没有重新调用 `dmc_board_init_eth` 或 `dmc_board_init` 建立连接
6. 再次检查总线状态
7. 设置 `_isAvailable = true` 和 `_isInitialized = true`

**为什么会导致错误码 9**:
- 雷赛 API 要求：软复位后必须重新调用初始化函数
- 官方示例代码明确展示了这一点：
  ```csharp
  LTDMC.dmc_soft_reset(usCardNum);
  LTDMC.dmc_board_close();
  Thread.Sleep(15000);
  LTDMC.dmc_board_init_eth(usCardNum, "192.168.5.11");
  ```
- 我们的代码在软复位后没有重新建立连接，导致后续 IO 操作返回错误码 9

### 问题2: 数递鸟心跳检查问题

**根本原因**:  
使用 `TcpClient.Connected` 属性判断连接状态，但该属性**只反映上一次同步操作的状态**，不能实时反映 TCP 连接的真实状态。

**详细原因**:
- `TcpClient.Connected` 是一个缓存属性，只在执行 I/O 操作时更新
- 如果连接在上次 I/O 操作后断开，该属性仍然返回 `true`
- 反之，如果连接已恢复但尚未进行 I/O 操作，该属性仍然返回 `false`
- 这是 .NET 的已知设计限制

**原始代码**:
```csharp
var isConnected = _tcpClient?.Connected == true && _stream != null;

if (!isConnected)
{
    _logger.LogDebug("摆轮 {DiverterId} 心跳检查: TCP连接断开", DiverterId);
    return Task.FromResult(false);
}
```

---

## 解决方案

### 修复1: 雷赛软复位后重新初始化连接

**修改位置**: `src/Drivers/.../Leadshine/LeadshineEmcController.cs`

**关键改进**:
1. 在软复位后，显式关闭连接（`dmc_board_close()`）
2. 根据配置模式重新调用 `dmc_board_init_eth` 或 `dmc_board_init`
3. 验证重新初始化是否成功
4. 再次检查总线状态确认恢复

### 修复2: 数递鸟心跳检查使用接收任务状态

**修改位置**: `src/Drivers/.../ShuDiNiao/ShuDiNiaoWheelDiverterDriver.cs`

**关键改进**:
1. 使用 `_receiveTask != null && !_receiveTask.IsCompleted` 判断接收任务是否正在运行
2. 使用 `_stream != null` 判断网络流是否存在
3. 优先通过接收任务运行状态和心跳时间来判断连接是否正常
4. 增强日志输出，包含接收任务和流的状态信息，便于诊断

### 修复3: 增强 IO 操作诊断日志

**修改位置**: `src/Drivers/.../Leadshine/LeadshineIoLinkageDriver.cs`

**修改内容**:
- 在 `SetIoPointAsync` 和 `ReadIoPointAsync` 中添加详细的诊断日志
- 记录 `CardNo`、`BitNumber`、`OutputValue` 等关键参数
- 针对错误码 9 提供详细的错误分析和可能原因

---

## 测试验证

### 单元测试

```bash
# 运行数递鸟相关测试
dotnet test --filter "FullyQualifiedName~ShuDiNiao"
# 结果: Passed: 66, Failed: 0

# 运行雷赛相关测试
dotnet test --filter "FullyQualifiedName~Leadshine"
# 结果: Passed: 9, Failed: 0
```

---

## 影响范围

### 修改的文件
1. `src/Drivers/.../Leadshine/LeadshineEmcController.cs` - EMC 控制器初始化逻辑
2. `src/Drivers/.../Leadshine/LeadshineIoLinkageDriver.cs` - IO 联动驱动诊断日志
3. `src/Drivers/.../ShuDiNiao/ShuDiNiaoWheelDiverterDriver.cs` - 心跳检查逻辑

### 向后兼容性
- ✅ 完全向后兼容
- ✅ 现有测试全部通过
- ✅ 不影响其他厂商驱动

---

## 经验总结

### 技术要点

1. **雷赛 API 使用规范**:
   - 软复位后必须重新调用初始化函数
   - `dmc_board_close()` 是全局操作，会关闭所有控制卡
   - 必须严格遵循"关闭→等待→重新初始化"的流程

2. **TCP 连接状态判断**:
   - 不能依赖 `TcpClient.Connected` 属性判断实时连接状态
   - 应该通过接收任务状态、心跳时间等多重指标综合判断
   - 对于有状态上报的协议，优先使用心跳机制

3. **诊断日志的重要性**:
   - 关键操作前后必须记录详细的参数信息
   - 错误码应该提供可能原因和排查建议
   - 日志级别应该根据严重程度合理设置

---

**文档版本**: 1.0  
**最后更新**: 2025-12-08  
**维护人员**: GitHub Copilot
