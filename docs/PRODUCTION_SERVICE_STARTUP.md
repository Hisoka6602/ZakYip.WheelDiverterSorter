# 生产环境服务启动说明

本文档说明系统在生产环境中的服务启动流程和配置加载机制。

> **Windows Service 部署**: 如需将系统部署为 Windows Service，请参阅 [Windows Service 部署指南](./WINDOWS_SERVICE_DEPLOYMENT.md)
> 
> **重要提示**: Windows Service 部署必须使用 **Release 构建**。使用 Debug 构建会导致服务启动失败（错误 1053）。

## 目录

1. [服务启动流程](#服务启动流程)
2. [配置加载机制](#配置加载机制)
3. [日志验证](#日志验证)
4. [故障排查](#故障排查)

---

## 服务启动流程

### 1. 主机启动 (Program.cs)

```
Program.cs
  ↓
AddWheelDiverterSorterHost(configuration)
  ↓
注册所有服务和后台工作器
```

### 2. 后台服务自动启动

系统启动时，以下后台服务会自动启动：

| 服务名称 | 职责 | 启动时机 |
|---------|------|---------|
| `SortingServicesInitHostedService` | 启动分拣服务、传感器监听、上游连接 | 应用启动时 |
| `WheelDiverterHeartbeatMonitor` | 监控摆轮心跳状态 | 应用启动时 |
| `AlarmMonitoringWorker` | 监控告警状态 | 应用启动时 |
| `PanelButtonMonitorWorker` | 监控面板按钮 | 应用启动时 |
| `RouteTopologyConsistencyCheckWorker` | 检查路由拓扑一致性 | 应用启动时 |

### 3. 分拣服务启动链

```
SortingServicesInitHostedService.StartAsync()
  ↓
ISortingOrchestrator.StartAsync()
  ↓
ISensorEventProvider.StartAsync()
  ↓
SensorEventProviderAdapter.StartAsync()
  ↓
IParcelDetectionService.StartAsync()
  ↓
- 订阅所有传感器事件
- 启动每个传感器的轮询
- 启动传感器健康监控
```

### 4. 传感器启动

对于每个配置的传感器：

```
ISensor.StartAsync()
  ↓
- 开始轮询硬件IO端口
- 监听物体遮挡事件
- 触发 SensorTriggered 事件
  ↓
ParcelDetectionService.OnSensorTriggered()
  ↓
- 生成唯一包裹ID
- 触发 ParcelDetected 事件
  ↓
SensorEventProviderAdapter.OnUnderlyingParcelDetected()
  ↓
- 转换事件类型
- 转发到 Execution 层
  ↓
SortingOrchestrator.OnParcelDetected()
  ↓
开始分拣流程
```

---

## 配置加载机制

### 配置存储位置

**重要**: 所有业务配置都存储在数据库中，**不在 appsettings.json 中**。

- **数据库路径**: `Data/routes.db` (LiteDB)
- **配置类型**: 
  - 系统配置 (SystemConfiguration)
  - 驱动配置 (DriverConfiguration)
  - 传感器配置 (SensorConfiguration)
  - 摆轮配置 (WheelDiverterConfiguration)
  - 通信配置 (CommunicationConfiguration)
  - 拓扑配置 (ChutePathTopologyConfig)
  - 面板配置 (PanelConfiguration)
  - IO联动配置 (IoLinkageConfiguration)

### 配置加载时机

| 配置类型 | 加载时机 | 加载方式 |
|---------|---------|---------|
| **传感器配置** | 应用启动时 | `ISensorVendorConfigProvider.GetSensorConfigs()` |
| **拓扑配置** | 首次生成路径时 | `IChutePathTopologyRepository.Get()` |
| **摆轮配置** | 应用启动时 | `IWheelDiverterConfigurationRepository.Get()` |
| **驱动配置** | 应用启动时 | `IDriverConfigurationRepository.Get()` |
| **系统配置** | 按需读取 | `ISystemConfigurationRepository.Get()` |

### 配置管理方式

**通过 API 端点管理** (推荐):

```bash
# 获取传感器配置
GET /api/config/sensors

# 更新传感器配置
PUT /api/config/sensors

# 获取拓扑配置
GET /api/config/topology

# 更新拓扑配置
PUT /api/config/topology

# 获取摆轮配置
GET /api/config/wheel-diverters

# 更新摆轮配置
PUT /api/config/wheel-diverters
```

**配置修改生效方式**:
- 大部分配置需要**重启应用**才能生效
- 部分配置支持热更新（通过 API 文档查看）

---

## 日志验证

### 启动成功的日志标志

#### 1. ParcelDetectionService 启动日志

```
========== 包裹检测服务启动完成 ==========
  - 已注册传感器: 2 个
  - 已激活传感器: 2 个
  - 健康监控: 已启用
  - 服务状态: 运行中，等待包裹触发事件
========================================
```

**日志级别**: Information  
**日志来源**: `ZakYip.WheelDiverterSorter.Ingress.Services.ParcelDetectionService`

#### 2. 拓扑配置加载日志

```
========== 使用拓扑配置生成路径 ==========
  - 拓扑名称: 默认3摆轮拓扑
  - 拓扑ID: default-topology
  - 摆轮节点数: 3
  - 异常格口ID: 999
  - 配置来源: 数据库 (Production)
=========================================
```

**日志级别**: Debug (System.Diagnostics.Debug)  
**日志来源**: `ZakYip.WheelDiverterSorter.Core.LineModel.Topology.DefaultSwitchingPathGenerator`

#### 3. 传感器启动日志

```
启动包裹检测服务
传感器 SENSOR_PE_01 (Photoelectric) 已启动
传感器 SENSOR_LASER_01 (Laser) 已启动
传感器健康监控已启动
```

**日志级别**: Information  
**日志来源**: `ZakYip.WheelDiverterSorter.Ingress.Services.ParcelDetectionService`

#### 4. 分拣服务启动日志

```
========== 分拣服务初始化 ==========
正在启动分拣编排服务...
正在启动传感器事件监听...
传感器事件监听已启动
✅ 分拣服务初始化完成
  - 传感器监听已启动并开始轮询
  - 上游连接已建立或将在首次使用时建立
  - 分拣编排服务已就绪
=======================================
```

**日志级别**: Information  
**日志来源**: `ZakYip.WheelDiverterSorter.Host.Services.Workers.SortingServicesInitHostedService`

#### 5. 摆轮心跳监控日志

```
========== 摆轮心跳监控服务已启动 ==========
```

**日志级别**: Information  
**日志来源**: `ZakYip.WheelDiverterSorter.Host.Services.Workers.WheelDiverterHeartbeatMonitor`

**心跳异常日志** (最小间隔60秒):
```
摆轮 1 心跳检查失败
摆轮 1 心跳超时！最后成功时间: 2025-12-10 14:00:00, 已超时: 00:01:15
```

---

## 故障排查

### 问题 1: 包裹检测服务未启动

**症状**: 
- 日志中找不到 "包裹检测服务启动完成"
- 传感器触发没有反应

**可能原因**:
1. 传感器配置缺失或禁用
2. 硬件IO端口未连接
3. 驱动初始化失败

**排查步骤**:
```bash
# 1. 检查传感器配置
curl http://localhost:5000/api/config/sensors

# 2. 检查驱动配置
curl http://localhost:5000/api/config/driver

# 3. 查看启动日志
# 查找 "传感器" 关键字
grep "传感器" logs/*.log
```

### 问题 2: 拓扑配置未加载

**症状**:
- 包裹分拣失败，总是走异常口
- 日志中找不到 "使用拓扑配置生成路径"

**可能原因**:
1. 拓扑配置未初始化
2. 拓扑配置数据损坏
3. 没有触发路径生成

**排查步骤**:
```bash
# 1. 检查拓扑配置
curl http://localhost:5000/api/config/topology

# 2. 查看路径生成日志
# 查找 "拓扑" 关键字
grep "拓扑" logs/*.log

# 3. 手动触发分拣流程测试
# (通过调试API或触发传感器)
```

### 问题 3: 摆轮心跳异常频繁报警

**症状**:
- 日志中频繁出现 "心跳检查失败"
- 每60秒记录一次异常

**可能原因**:
1. 摆轮设备未连接或断电
2. 网络不通
3. 摆轮驱动配置错误

**排查步骤**:
```bash
# 1. 检查摆轮配置
curl http://localhost:5000/api/config/wheel-diverters

# 2. 检查网络连通性
ping <摆轮IP地址>

# 3. 查看心跳监控日志
grep "心跳" logs/*.log | tail -20
```

### 问题 4: 传感器不触发

**症状**:
- 传感器已启动，但物体通过时无反应
- 没有 "检测到包裹" 日志

**可能原因**:
1. 传感器IO位配置错误
2. 传感器硬件故障
3. 轮询间隔过长

**排查步骤**:
```bash
# 1. 检查传感器配置
curl http://localhost:5000/api/config/sensors

# 2. 查看传感器状态
# 查找特定传感器ID
grep "SENSOR_PE_01" logs/*.log | tail -20

# 3. 检查IO端口状态
# (通过驱动调试接口或硬件诊断工具)
```

---

## 配置示例

### 最小可用配置

系统启动至少需要以下配置：

1. **驱动配置** - 指定IO驱动厂商
2. **传感器配置** - 至少1个启用的传感器
3. **拓扑配置** - 定义摆轮和格口拓扑
4. **系统配置** - 异常格口ID等基本参数

### 配置初始化

首次启动时，系统会自动创建默认配置：

```csharp
// 在 AddConfigurationRepositories() 中自动调用
repository.InitializeDefault();
```

---

## 相关文档

- [系统配置指南](./guides/SYSTEM_CONFIG_GUIDE.md)
- [传感器IO轮询配置](./guides/SENSOR_IO_POLLING_CONFIGURATION.md)
- [拓扑配置模型](./TOPOLOGY_LINEAR_N_DIVERTERS.md)
- [API使用指南](./guides/API_USAGE_GUIDE.md)

---

**维护团队**: ZakYip Development Team  
**最后更新**: 2025-12-10
