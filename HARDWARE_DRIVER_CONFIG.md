# 硬件驱动器配置指南

本文档说明如何配置和使用WheelDiverterSorter的硬件驱动器功能。

## 概述

系统支持两种执行器模式：
- **模拟模式** (`UseHardwareDriver: false`): 用于开发和测试，不需要实际硬件
- **硬件模式** (`UseHardwareDriver: true`): 连接实际的雷赛控制器，控制真实摆轮设备

## 配置文件

配置位于 `appsettings.json` 的 `Driver` 节点下。

### 模拟模式配置（开发环境）

```json
{
  "Driver": {
    "UseHardwareDriver": false
  }
}
```

在模拟模式下，系统将使用 `MockSwitchingPathExecutor`，不需要配置硬件参数。

### 硬件模式配置（生产环境）

```json
{
  "Driver": {
    "UseHardwareDriver": true,
    "Leadshine": {
      "CardNo": 0,
      "Diverters": [
        {
          "DiverterId": "D1",
          "OutputStartBit": 0,
          "FeedbackInputBit": 10
        },
        {
          "DiverterId": "D2",
          "OutputStartBit": 2,
          "FeedbackInputBit": 11
        },
        {
          "DiverterId": "D3",
          "OutputStartBit": 4,
          "FeedbackInputBit": 12
        }
      ]
    }
  }
}
```

## 配置参数说明

### Driver 节点

| 参数 | 类型 | 必填 | 说明 | 默认值 |
|------|------|------|------|--------|
| UseHardwareDriver | bool | 是 | 是否使用硬件驱动器 | false |
| Leadshine | object | 否 | 雷赛控制器配置（仅在UseHardwareDriver=true时需要） | - |

### Leadshine 节点

| 参数 | 类型 | 必填 | 说明 | 默认值 |
|------|------|------|------|--------|
| CardNo | number | 是 | 雷赛控制器卡号 | 0 |
| Diverters | array | 是 | 摆轮配置数组 | [] |

### Diverters 数组元素

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| DiverterId | string | 是 | 摆轮唯一标识，必须与路由配置中的DiverterId一致 |
| OutputStartBit | number | 是 | 输出起始位索引，需要连续2个位用于角度编码 |
| FeedbackInputBit | number | 否 | 反馈输入位索引，用于读取摆轮状态 |

## 输出位规划

每个摆轮需要2个连续的输出位进行角度控制：

| 摆轮 | 起始位 | 使用的位 | 角度编码 |
|------|--------|----------|----------|
| D1 | 0 | 0, 1 | Bit0控制30°/0°, Bit1控制45°/0° |
| D2 | 2 | 2, 3 | Bit2控制30°/0°, Bit3控制45°/0° |
| D3 | 4 | 4, 5 | Bit4控制30°/0°, Bit5控制45°/0° |

### 角度编码表

| 角度 | Bit1 | Bit0 | 说明 |
|------|------|------|------|
| 0° | 0 | 0 | 直行 |
| 30° | 0 | 1 | 小角度分流 |
| 45° | 1 | 0 | 中角度分流 |
| 90° | 1 | 1 | 大角度分流 |

## 硬件连接

### 雷赛控制器要求

- 型号：LTDMC系列运动控制卡
- 操作系统：Windows（LTDMC.dll仅支持Windows）
- 连接方式：USB/以太网

### IO端口连接

输出端口接线示例（以D1为例）：

```
控制器输出位0 → 摆轮D1的Bit0控制线
控制器输出位1 → 摆轮D1的Bit1控制线
控制器输入位10 → 摆轮D1的反馈信号线（可选）
```

## 配置步骤

### 1. 安装硬件驱动

确保已安装雷赛LTDMC控制器驱动程序。

### 2. 确认硬件连接

- 控制器正常上电
- USB/网络连接正常
- IO接线正确

### 3. 修改配置文件

编辑 `appsettings.json`，设置：
- `UseHardwareDriver: true`
- 配置正确的 `CardNo`
- 根据实际接线配置 `Diverters` 数组

### 4. 更新路由配置

确保路由配置中的 `DiverterId` 与硬件配置一致。

通过API更新配置：

```bash
curl -X POST http://localhost:5000/api/config/routes \
  -H "Content-Type: application/json" \
  -d '{
    "chuteId": "CHUTE_A",
    "diverterConfigurations": [
      {"diverterId": "D1", "targetAngle": 30, "sequenceNumber": 1},
      {"diverterId": "D2", "targetAngle": 45, "sequenceNumber": 2}
    ],
    "isEnabled": true
  }'
```

### 5. 重启应用

配置更改后需要重启应用程序使其生效。

## 测试

### 测试模拟模式

```bash
curl -X POST http://localhost:5000/api/debug/sort \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "TEST001", "targetChuteId": "CHUTE_A"}'
```

预期响应：
```json
{
  "parcelId": "TEST001",
  "targetChuteId": "CHUTE_A",
  "isSuccess": true,
  "actualChuteId": "CHUTE_A",
  "message": "分拣成功",
  "pathSegmentCount": 2
}
```

### 测试硬件模式

1. 确保 `UseHardwareDriver: true`
2. 启动应用，观察日志：
   ```
   已初始化硬件摆轮路径执行器，管理 3 个摆轮
   ```
3. 调用同样的API测试
4. 观察摆轮实际动作

## 故障排查

### 问题：找不到LTDMC.dll

**现象**: 
```
DllNotFoundException: Unable to load DLL 'LTDMC.dll'
```

**解决方案**:
1. 检查DLL文件是否存在于输出目录
2. 确认操作系统架构（x64/x86）
3. 验证.NET运行时版本

### 问题：控制器连接失败

**现象**:
```
设置摆轮 D1 角度失败
写入输出位失败，错误码: XXX
```

**解决方案**:
1. 确认控制器已上电
2. 检查USB/网络连接
3. 验证CardNo配置正确
4. 使用雷赛官方软件测试控制器

### 问题：摆轮不响应

**现象**: 日志显示成功，但摆轮无动作

**解决方案**:
1. 确认OutputStartBit配置正确
2. 检查IO端口接线
3. 使用万用表测试输出信号
4. 验证摆轮电源供应

### 问题：配置不生效

**现象**: 修改配置后行为未改变

**解决方案**:
1. 确认已重启应用程序
2. 检查配置文件格式（JSON语法）
3. 查看启动日志确认配置加载
4. 验证环境变量没有覆盖配置

## 日志监控

启用详细日志以便调试：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ZakYip.WheelDiverterSorter.Drivers": "Debug"
    }
  }
}
```

关键日志示例：

- 成功: `摆轮 D1 已设置为 30度`
- 失败: `段 1 执行失败，摆轮=D1`
- 超时: `段 1 执行超时（TTL=5000ms）`

## 性能优化

### 减少IO延迟

当前实现在设置每个角度后有100ms延迟，可根据实际硬件响应时间调整：

```csharp
// LeadshineDiverterController.cs
await Task.Delay(50, cancellationToken); // 调整此值
```

### 批量操作

如果多个摆轮需要同时动作，可考虑实现批量写入：

```csharp
// 未来优化：实现IOutputPort.WriteBatchAsync
await outputPort.WriteBatchAsync(startBit, values);
```

## 安全注意事项

1. **急停机制**: 确保物理急停按钮可以立即断开摆轮电源
2. **互锁保护**: 实现软件互锁，防止冲突指令
3. **状态监控**: 实时监控摆轮状态，及时发现异常
4. **错误恢复**: 设置合理的TTL值，避免死锁

## 参考文档

- [Drivers项目README](../ZakYip.WheelDiverterSorter.Drivers/README.md)
- [Leadshine驱动器文档](../ZakYip.WheelDiverterSorter.Drivers/Leadshine/README.md)
- [配置管理API文档](CONFIGURATION_API.md)
- [Singulation参考项目](https://github.com/Hisoka6602/ZakYip.Singulation)
