# ZakYip.WheelDiverterSorter

直线摆轮分拣系统

## ⚠️ 重要说明

本程序和 [ZakYip.Sorting.RuleEngine.Core](https://github.com/Hisoka6602/ZakYip.Sorting.RuleEngine.Core) 是**分开部署**的两个独立程序，互为上下游关系：

- **ZakYip.WheelDiverterSorter（本项目，下游）**：
  - 通过**IO传感器**感应物理包裹并创建包裹记录
  - 向RuleEngine请求格口号
  - 负责**实际执行分拣**动作（控制摆轮）

- **ZakYip.Sorting.RuleEngine.Core（上游）**：
  - 接收包裹ID请求
  - 与**DWS第三方API**通信获取包裹信息（条码、重量、尺寸等）
  - 通过规则引擎决策目标格口号
  - 返回格口号给WheelDiverterSorter

**通信方式：** 两个系统通过**TCP/SignalR/MQTT**等协议通信（多选一）
- ✅ **生产环境推荐**：TCP、SignalR或MQTT（高性能、长连接）
- ❌ **生产环境禁止**：HTTP（仅用于模拟测试，性能不足）

详细的系统关系和集成方案请参阅：[与规则引擎的关系文档](RELATIONSHIP_WITH_RULEENGINE.md)

---

## 项目简介

本项目是一个基于直线摆轮（Wheel Diverter）的包裹自动分拣系统。通过在直线输送线上配置多个摆轮节点，实现包裹到不同格口的智能分拣。系统采用分层架构设计，支持路径规划、执行和可观测性。

## 项目当前完成度

### ✅ 已完成功能

- **核心路径生成逻辑** (100%)
  - 实现了基于格口到摆轮映射的路径生成器 (`DefaultSwitchingPathGenerator`)
  - 支持多段摆轮路径生成，每段包含摆轮ID、目标角度和TTL
  - 定义了完整的拓扑结构模型 (`SorterTopology`, `DiverterNode`)

- **模拟执行器** (100%)
  - 实现了模拟执行器 (`MockSwitchingPathExecutor`)
  - 支持异步执行和取消操作
  - 包含基本的错误处理和异常格口回退机制

- **调试Web接口** (100%)
  - 提供HTTP API端点用于手动触发分拣测试 (`POST /api/debug/sort`)
  - 包含完整的请求验证和响应模型
  - 集成了日志注入防护

- **基础架构** (100%)
  - 采用依赖注入（DI）设计模式
  - 定义了清晰的接口抽象（`ISwitchingPathGenerator`, `ISwitchingPathExecutor`）
  - 支持ASP.NET Core Minimal API

### 🚧 部分完成功能

- **拓扑配置** (70%)
  - 已定义拓扑数据模型，但当前路径生成器使用硬编码映射
  - 缺少从配置文件或数据库动态加载拓扑的功能
  - 已提供示例拓扑提供者 (`DefaultSorterTopologyProvider`)

- **入口管理** (30%)
  - 项目结构中已创建Ingress层，但功能未实现
  - **需要开发**：IO传感器监听模块，用于感应包裹并创建包裹记录
  - 缺少扫码触发和供包台触发等生产环境入口

- **可观测性** (30%)
  - 项目结构中已创建Observability层，但功能未实现
  - 当前仅有基础日志记录，缺少指标收集、链路追踪等

- **通信层** (0%)
  - **需要开发**：与RuleEngine.Core的通信客户端
  - **需要支持**：TCP/SignalR/MQTT等多种协议
  - 当前仅有HTTP调试接口（生产环境禁用）

### ❌ 未完成功能

- **IO传感器集成**
  - 需要实现物理传感器监听（光电、激光等）
  - 包裹到达时触发创建事件
  - 生成唯一包裹ID

- **与RuleEngine通信**
  - 实现TCP/SignalR/MQTT客户端
  - 向RuleEngine请求格口号
  - 处理连接管理和错误重试

- **真实设备集成**
  - 当前仅有模拟执行器，无真实PLC/设备通信模块
  - 缺少设备状态监控和故障检测

- **高级路径算法**
  - 当前使用简单的硬编码映射，不支持动态路径优化
  - 不支持基于拓扑图的自动路径搜索

- **生产环境功能**
  - 无扫码触发集成
  - 无供包台接口
  - 缺少包裹跟踪和历史记录
  - 缺少异常告警机制

- **测试覆盖**
  - 项目中无单元测试或集成测试

## 🎯 后续开发方向和计划

### 核心目标：实现与RuleEngine.Core的完整集成

为了实现完整的自动分拣流程，需要按以下优先级进行开发：

### 阶段1：IO传感器集成（高优先级） ⏰ 预计2-3周

**目标**：实现包裹的自动感应和创建

**任务清单**：
- [ ] **实现IO传感器监听模块**（Ingress层）
  - 支持光电传感器、激光传感器等
  - 传感器事件触发机制
  - 包裹到达检测逻辑
  
- [ ] **包裹创建流程**
  - 自动生成唯一包裹ID
  - 创建包裹记录到本地数据库
  - 记录包裹到达时间和传感器位置
  
- [ ] **配置管理**
  - 传感器配置（端口、类型、灵敏度等）
  - 支持热更新配置

**修改涉及的文件**：
- `ZakYip.WheelDiverterSorter.Ingress/` 新增传感器监听模块
- `ZakYip.WheelDiverterSorter.Core/Models/` 完善包裹数据模型
- `appsettings.json` 添加传感器配置

### 阶段2：通信层开发（高优先级） ⏰ 预计3-4周

**目标**：实现与RuleEngine.Core的网络通信

**任务清单**：
- [ ] **实现多协议通信客户端**
  - TCP Socket客户端（推荐生产环境）
  - SignalR客户端（推荐生产环境）
  - MQTT客户端（推荐生产环境）
  - HTTP客户端（仅供测试）
  
- [ ] **请求格口号接口**
  - 发送包裹ID到RuleEngine
  - 等待接收格口号响应
  - 超时和重试机制
  
- [ ] **连接管理**
  - 自动连接和断线重连
  - 心跳检测
  - 连接状态监控
  
- [ ] **配置切换**
  - 支持通过配置文件切换通信协议
  - 不同环境使用不同协议（测试用HTTP，生产用TCP/SignalR/MQTT）

**修改涉及的文件**：
- 新增 `ZakYip.WheelDiverterSorter.Communication/` 通信层项目
- `ZakYip.WheelDiverterSorter.Core/Interfaces/` 定义通信接口
- `ZakYip.WheelDiverterSorter.Host/Program.cs` 注册通信服务
- `appsettings.json` 添加RuleEngine连接配置

**关键配置示例**：
```json
{
  "RuleEngineConnection": {
    "Mode": "TCP",  // 可选: TCP, SignalR, MQTT, HTTP
    "TcpServer": "192.168.1.100:8000",
    "SignalRHub": "http://192.168.1.100:5000/sortingHub",
    "MqttBroker": "mqtt://192.168.1.100:1883",
    "HttpApi": "http://localhost:5000/api/sorting/chute",
    "TimeoutMs": 5000,
    "RetryCount": 3
  }
}
```

### 阶段3：整合与测试（中优先级） ⏰ 预计2-3周

**目标**：将IO传感器和通信层整合到完整流程

**任务清单**：
- [ ] **完整流程集成**
  - IO传感器检测 → 创建包裹 → 请求格口号 → 执行分拣
  - 异常处理和降级策略
  - 日志记录和追踪
  
- [ ] **联调测试**
  - 与RuleEngine.Core进行端到端测试
  - 模拟各种异常场景
  - 性能和压力测试
  
- [ ] **文档更新**
  - 更新部署文档
  - 编写运维手册
  - API接口文档

### 阶段4：生产环境功能（低优先级） ⏰ 预计持续开发

- [ ] 真实PLC/设备集成
- [ ] 可观测性增强（Prometheus、Grafana）
- [ ] 单元测试和集成测试
- [ ] 性能优化和并发控制

### 关键修改方向总结

| 优先级 | 功能模块 | 涉及项目/文件 | 预计工期 |
|-------|---------|-------------|---------|
| 🔴 **最高** | IO传感器监听 | Ingress层 | 2-3周 |
| 🔴 **最高** | 通信层开发 | 新增Communication项目 | 3-4周 |
| 🟡 **中等** | 整合测试 | Host、Core | 2-3周 |
| 🟢 **低** | 生产功能 | 各层 | 持续开发 |

**总体时间线**：完成核心功能（阶段1-3）预计需要 **7-10周**

**成功标准**：
- ✅ IO传感器能自动感应包裹并创建记录
- ✅ 系统能通过TCP/SignalR/MQTT与RuleEngine通信
- ✅ 完整的包裹分拣流程可以端到端运行
- ✅ 生产环境禁用HTTP，仅用TCP/SignalR/MQTT

详细的系统关系和集成方案请参阅：[与规则引擎的关系文档](RELATIONSHIP_WITH_RULEENGINE.md)

---

## 项目结构

- **ZakYip.WheelDiverterSorter.Core**: 核心业务逻辑，包含路径生成器接口和实现
- **ZakYip.WheelDiverterSorter.Execution**: 执行层，包含路径执行器接口和模拟实现
- **ZakYip.WheelDiverterSorter.Host**: Web API 主机，提供调试接口
- **ZakYip.WheelDiverterSorter.Ingress**: 入口管理（**待实现**：IO传感器监听）
- **ZakYip.WheelDiverterSorter.Observability**: 可观测性支持（待实现）
- **ZakYip.WheelDiverterSorter.Communication**: 通信层（**待创建**：与RuleEngine通信）

## 项目运行流程

### 系统启动流程

1. **启动Web API主机**
   ```bash
   cd ZakYip.WheelDiverterSorter.Host
   dotnet run
   ```
   - 默认监听端口：5000（HTTP）
   - 自动注册依赖服务（路径生成器、执行器）

2. **系统初始化**
   - 加载摆轮拓扑配置（当前使用硬编码映射）
   - 初始化路径生成器和执行器
   - 启动Web API监听

### 包裹分拣流程

#### 完整工作流程（生产环境）

```
IO传感器感应 → 创建包裹 → 请求格口号(TCP/SignalR/MQTT) → RuleEngine决策 
→ 接收格口号 → 生成摆轮路径 → 执行路径 → 到达目标格口
```

#### 当前流程（测试环境）

```
手动触发HTTP API → 生成摆轮路径 → 执行路径 → 到达目标格口
```

#### 详细步骤说明

1. **包裹入口**
   - **生产模式（待实现）**：
     - IO传感器感应包裹到达
     - 自动创建包裹记录并生成包裹ID
     - 通过TCP/SignalR/MQTT向RuleEngine.Core请求格口号
     - RuleEngine与DWS第三方API通信获取包裹信息
     - RuleEngine返回目标格口ID
   - **调试模式（当前实现）**：
     - 通过HTTP API手动触发 `POST /api/debug/sort`
     - 直接提供包裹ID和目标格口ID
     - ⚠️ 此模式仅用于测试，生产环境禁用HTTP

2. **路径生成阶段** (`ISwitchingPathGenerator`)
   - 接收包裹ID和目标格口ID
   - 查询格口到摆轮的映射关系
   - 生成有序的摆轮路径段列表（`SwitchingPath`）
   - 每段包含：摆轮ID、目标角度、TTL（超时时间）
   - 如果格口未配置，返回null，包裹将走异常口

3. **路径执行阶段** (`ISwitchingPathExecutor`)
   - 按顺序执行每个路径段
   - 模拟执行器：模拟设备响应延迟
   - 真实执行器（待实现）：与PLC/设备通信，控制摆轮角度
   - 监控每段的TTL，超时则标记失败

4. **结果反馈**
   - 执行成功：返回目标格口ID
   - 执行失败：返回异常格口ID和失败原因
   - 记录执行日志供后续分析

## 工作原理

### 核心概念

#### 1. 摆轮（Wheel Diverter）

摆轮是安装在直线输送线上的分拣设备，通过旋转不同角度将包裹分流到不同方向：

- **0度**：包裹直行通过
- **30度/45度**：小角度分流（常用于相邻格口）
- **90度**：大角度分流（常用于垂直分拣）

#### 2. 直线拓扑结构

```
入口 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 末端
        ↓         ↓         ↓
     格口B      格口A     格口C
```

包裹在输送线上单向移动，经过配置的摆轮节点时，根据路径指令分流到目标格口。

#### 3. 路径规划

**当前实现（硬编码映射）**：
```csharp
// 格口A：需要经过摆轮D1（30度）和摆轮D2（45度）
"CHUTE_A" -> [D1:30°, D2:45°]

// 格口B：需要经过摆轮D1（0度直行）
"CHUTE_B" -> [D1:0°]

// 格口C：需要经过摆轮D1（90度）和摆轮D3（30度）
"CHUTE_C" -> [D1:90°, D3:30°]
```

**未来实现（基于拓扑的动态路径搜索）**：
- 从拓扑模型 (`SorterTopology`) 自动计算最优路径
- 支持多条路径选择和负载均衡
- 考虑设备状态和故障节点

#### 4. 路径段与TTL

每个路径段包含：
- **摆轮ID**：指定哪个摆轮执行动作
- **目标角度**：摆轮应旋转到的角度
- **TTL（Time To Live）**：该段的最大执行时间（默认5000ms）

如果段执行超过TTL，视为失败，包裹将被引导到异常格口。

#### 5. 异常处理机制

- **路径生成失败**：目标格口未配置 → 返回null → 包裹走异常口
- **路径执行失败**：段超时或设备故障 → 返回失败结果 → 包裹走异常口
- **异常格口**：默认为 `CHUTE_EXCEPTION`，用于收集所有异常包裹

## 调试接口

### 概述

Host 层提供了一个用于调试直线摆轮方案的最小接口。

**注意**：这是调试入口，正式环境可改成由扫码触发或供包台触发。

### API 端点

**POST** `/api/debug/sort`

#### 请求参数

```json
{
  "parcelId": "包裹ID",
  "targetChuteId": "目标格口ID"
}
```

#### 响应示例

成功案例：
```json
{
  "parcelId": "PKG001",
  "targetChuteId": "CHUTE_A",
  "isSuccess": true,
  "actualChuteId": "CHUTE_A",
  "message": "分拣成功：包裹 PKG001 已成功分拣到格口 CHUTE_A",
  "failureReason": null,
  "pathSegmentCount": 2
}
```

失败案例（未知格口）：
```json
{
  "parcelId": "PKG004",
  "targetChuteId": "CHUTE_UNKNOWN",
  "isSuccess": false,
  "actualChuteId": "未知",
  "message": "路径生成失败：目标格口无法映射到任何摆轮组合",
  "failureReason": "目标格口未配置或不存在",
  "pathSegmentCount": 0
}
```

### 使用示例

```bash
curl -X POST http://localhost:5000/api/debug/sort \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "PKG001", "targetChuteId": "CHUTE_A"}'
```

### 工作流程

1. 接收包裹ID和目标格口ID
2. 调用路径生成器（`ISwitchingPathGenerator`）生成 `SwitchingPath`
3. 调用执行器（`ISwitchingPathExecutor`）执行路径
4. 返回执行结果和实际落格ID

### 预配置的格口

当前默认配置包含以下格口映射：

- **CHUTE_A**: 需要经过摆轮D1（30度）和摆轮D2（45度）
- **CHUTE_B**: 需要经过摆轮D1（0度直行）
- **CHUTE_C**: 需要经过摆轮D1（90度）和摆轮D3（30度）

**注意**: 这些配置存储在 LiteDB 数据库中，可以通过配置管理 API 动态修改。

## 配置管理 API

系统提供了完整的 RESTful API 用于动态管理格口到摆轮的路由配置，支持热更新（无需重启）。

### 主要功能

- ✅ **动态配置管理**: 通过 API 添加、修改、删除格口配置
- ✅ **热更新支持**: 配置更改立即生效，无需重启应用
- ✅ **数据持久化**: 使用 LiteDB 存储配置数据
- ✅ **配置验证**: 自动验证配置的正确性和完整性

### API 端点

| 方法 | 端点 | 说明 |
|------|------|------|
| GET | `/api/config/routes` | 获取所有路由配置 |
| GET | `/api/config/routes/{chuteId}` | 获取指定格口配置 |
| POST | `/api/config/routes` | 创建新的路由配置 |
| PUT | `/api/config/routes/{chuteId}` | 更新路由配置（热更新） |
| DELETE | `/api/config/routes/{chuteId}` | 删除路由配置 |

### 快速示例

```bash
# 创建新格口配置
curl -X POST http://localhost:5000/api/config/routes \
  -H "Content-Type: application/json" \
  -d '{
    "chuteId": "CHUTE_D",
    "diverterConfigurations": [
      {"diverterId": "D2", "targetAngle": 45, "sequenceNumber": 1},
      {"diverterId": "D3", "targetAngle": 90, "sequenceNumber": 2}
    ],
    "isEnabled": true
  }'

# 立即使用新配置（无需重启）
curl -X POST http://localhost:5000/api/debug/sort \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "PKG001", "targetChuteId": "CHUTE_D"}'
```

详细的 API 文档请参阅：[配置管理 API 文档](CONFIGURATION_API.md)

## 运行项目

```bash
cd ZakYip.WheelDiverterSorter.Host
dotnet run
```

默认监听端口：5000（HTTP）

## 存在的隐患和待改进项

### 🔴 高优先级隐患

#### 1. ~~硬编码配置依赖~~ ✅ 已解决
- **问题**：格口到摆轮的映射关系硬编码在 `DefaultSwitchingPathGenerator` 中
- **风险**：添加或修改格口需要重新编译和部署代码
- **影响**：生产环境灵活性差，维护成本高
- **解决方案**：
  - ✅ 使用 LiteDB 存储配置，支持动态管理
  - ✅ 提供完整的 RESTful API 进行配置管理
  - ✅ 支持热更新，配置更改立即生效无需重启
  - ✅ 详见 [配置管理 API 文档](CONFIGURATION_API.md)

#### 2. 缺少真实设备集成
- **问题**：当前仅有模拟执行器，无法控制真实PLC/摆轮设备
- **风险**：无法在实际生产环境中使用
- **影响**：系统仅停留在演示阶段
- **建议**：
  - 实现基于ModbusTCP/OPC UA/其他工业协议的真实执行器
  - 添加设备连接状态监控
  - 实现设备故障检测和自动重连

#### 3. 无单元测试和集成测试
- **问题**：项目中没有任何测试代码
- **风险**：代码重构或新增功能时易引入bug，难以保证质量
- **影响**：系统稳定性无法保障
- **建议**：
  - 为核心路径生成逻辑添加单元测试
  - 为API端点添加集成测试
  - 引入测试覆盖率工具

#### 4. 缺少并发控制
- **问题**：多个包裹同时请求同一摆轮时，可能产生冲突
- **风险**：摆轮角度切换频繁，降低分拣效率或导致分拣错误
- **影响**：高并发场景下系统不可用
- **建议**：
  - 实现摆轮资源锁机制
  - 添加包裹队列管理
  - 优化路径调度算法，批量处理相同目标的包裹

### 🟡 中优先级问题

#### 5. 静态TTL设置
- **问题**：所有路径段使用固定5000ms的TTL
- **风险**：无法适应不同距离的摆轮节点或不同速度的输送线
- **影响**：可能导致不必要的超时或资源浪费
- **建议**：
  - 根据摆轮位置和输送速度动态计算TTL
  - 支持在拓扑配置中为每个节点设置不同TTL
  - 添加TTL自适应调整机制

#### 6. 简单的路径生成算法
- **问题**：当前使用直接映射，不考虑设备状态和负载
- **风险**：无法处理设备故障、维护场景
- **影响**：缺少容错能力和优化空间
- **建议**：
  - 实现基于拓扑图的Dijkstra/A*路径搜索
  - 支持动态路径重规划
  - 考虑设备负载均衡

#### 7. 日志注入防护不完整
- **问题**：虽然实现了基本的日志清理，但可能不够全面
- **风险**：潜在的日志注入攻击风险
- **影响**：安全性问题
- **建议**：
  - 使用结构化日志（如Serilog）替代字符串拼接
  - 添加输入验证和白名单机制
  - 定期安全审计

#### 8. 缺少可观测性
- **问题**：无指标收集、链路追踪、告警机制
- **风险**：生产环境问题难以排查和监控
- **影响**：运维困难，故障响应慢
- **建议**：
  - 集成Prometheus/Grafana进行指标监控
  - 集成OpenTelemetry进行链路追踪
  - 添加关键指标：分拣成功率、平均响应时间、设备可用性等
  - 实现告警通知（钉钉/邮件/短信）

### 🟢 低优先级优化

#### 9. 缺少包裹跟踪和历史记录
- **问题**：无法查询包裹的历史分拣记录
- **影响**：问题溯源困难
- **建议**：
  - 添加数据库持久化
  - 记录每个包裹的完整路径和状态变更
  - 提供查询接口

#### 10. API文档不完整
- **问题**：缺少Swagger/OpenAPI文档
- **影响**：接口使用不便
- **建议**：
  - 集成Swashbuckle.AspNetCore
  - 生成交互式API文档

#### 11. 配置管理不灵活
- **问题**：异常格口ID、端口等配置硬编码
- **影响**：环境迁移不便
- **建议**：
  - 使用appsettings.json管理配置
  - 支持环境变量覆盖
  - 添加配置验证

#### 12. 缺少性能优化
- **问题**：未进行性能测试和优化
- **影响**：高吞吐量场景下性能未知
- **建议**：
  - 进行压力测试
  - 添加性能计数器
  - 优化热点代码路径

### 安全隐患总结

| 隐患类型 | 严重程度 | 是否已修复 | 备注 |
|---------|---------|-----------|------|
| 日志注入 | 中 | 部分 | 已有基础防护，建议增强 |
| 并发冲突 | 高 | 否 | 需要资源锁机制 |
| 配置泄露 | 中 | 否 | 建议使用密钥管理服务 |
| 输入验证 | 低 | 部分 | API有基础验证，建议完善 |

## 开发

### 构建

```bash
dotnet build
```

### 测试

```bash
dotnet test
```

注意：当前项目无测试，此命令不会执行任何测试。

## 技术栈

- **.NET 8.0**: 核心框架
- **ASP.NET Core Minimal API**: Web API框架
- **依赖注入（DI）**: 服务管理
- **Record类型**: 不可变数据模型

## 贡献指南

1. Fork本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建Pull Request

## 许可证

待定

## 联系方式

项目维护者：待补充
