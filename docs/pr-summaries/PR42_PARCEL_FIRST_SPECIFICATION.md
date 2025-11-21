# PR-42: Parcel-First 分拣语义规范

## 一、核心原则

本系统遵循 **Parcel-First（包裹优先）** 语义，作为分拣流程的基本架构约束。

### 定义

**Parcel-First** 是指：系统始终以本地包裹实体为中心，上游系统只对已存在的本地包裹执行路由分配。任何试图对不存在的 ParcelId 进行路由的行为都视为异常。

## 二、系统不变式（Invariants）

### Invariant 1: 上游请求必须引用已存在的本地包裹

```
∀ UpstreamRequest(parcelId):
    必须存在 LocalParcel(parcelId) 且 LocalParcel.CreatedAt < UpstreamRequest.SentAt
```

**含义**：
- 任何发送给上游的路由请求，必须引用一个已经存在的本地 ParcelId
- 请求发送时间必须晚于本地包裹创建时间

**违反后果**：
- 记录 Error 级别日志
- 请求不被发送
- 包裹被路由到异常格口

### Invariant 2: 上游响应必须匹配已存在的本地包裹

```
∀ UpstreamResponse(parcelId, chuteId):
    必须存在 LocalParcel(parcelId) 且 LocalParcel.CreatedAt < UpstreamResponse.ReceivedAt
```

**含义**：
- 本地不得接受/应用任何"找不到 ParcelId 对应包裹"的上游路由响应
- 响应接收时间必须晚于本地包裹创建时间

**违反后果**：
- 记录 Error 级别日志
- 响应被丢弃
- 不创建"幽灵包裹"

## 三、正常分拣闭环流程

### 3.1 完整流程步骤

```
1. 配置阶段
   └─> 通过 API 端点配置并启用 IO（传感器、按钮、摆轮等）

2. 启动阶段
   └─> 操作面板按下启动按钮
   └─> 线体进入可运行状态（Running）

3. 包裹检测 [本地创建包裹]
   └─> 包裹经输送线经过感应传感器
   └─> 本地立即创建包裹实体
   └─> 生成本地 ParcelId（毫秒时间戳）
   └─> 记录创建时间戳 t_created

4. 请求上游路由 [引用已存在的包裹]
   └─> 使用本地 ParcelId 向上游发送请求
   └─> 请求类型："已有包裹的路由查询/分配"
   └─> 记录请求时间戳 t_request
   └─> 校验：t_request > t_created

5. 接收上游响应 [绑定到已存在的包裹]
   └─> 上游返回 ParcelId + 目标格口号
   └─> 本地查找已有包裹实体
   └─> 验证包裹存在
   └─> 绑定路由信息到包裹
   └─> 记录绑定时间戳 t_bound
   └─> 校验：t_bound > t_request > t_created

6. 执行分拣
   └─> 根据路由引导摆轮执行分拣
   └─> 包裹落入目标格口
   └─> 记录落格时间戳 t_diverted
   └─> 校验：t_diverted > t_bound

7. 完成闭环
   └─> 记录落格结果
   └─> 更新包裹状态
```

### 3.2 时间顺序约束

```
严格时间顺序（Strict Total Order）:
t(ParcelCreated) < t(UpstreamRequestSent) < t(UpstreamReplyReceived) < t(RouteBound) < t(Diverted)
```

所有时间戳必须单调递增，不允许乱序。

## 四、异常流程处理

### 4.1 上游请求时包裹不存在

**场景**：尝试向上游发送请求时，本地找不到对应的 ParcelId

**处理**：
1. 记录 Error 日志：`"尝试为不存在的包裹 {ParcelId} 发送上游请求"`
2. 不发送上游请求
3. 返回失败结果
4. 通过 SafeExecutor 保证不崩溃

**示例日志**：
```
[ERROR] ParcelSortingOrchestrator: 尝试为不存在的包裹 1700000000123 发送上游请求，请求已阻止
```

### 4.2 上游响应时包裹不存在

**场景**：收到上游路由响应，但本地找不到对应的 ParcelId

**处理**：
1. 记录 Error 日志：`"收到未知包裹 {ParcelId} 的路由响应，可能是乱序或重放攻击"`
2. 丢弃响应
3. 可选：将 ParcelId 记入"未知包裹黑名单"，防止重复污染
4. 不创建新包裹实体

**示例日志**：
```
[ERROR] ParcelSortingOrchestrator: 收到未知包裹 1700000000456 的路由响应 (ChuteId=5)，响应已丢弃
```

### 4.3 超时处理

**场景**：上游在超时时间内未响应

**处理**：
1. 记录 Warning 日志
2. 包裹路由到异常格口
3. 保持包裹实体完整
4. 不违反 Parcel-First 语义

## 五、实现检查清单

### 5.1 代码层面

- [x] 上游请求入口：添加包裹存在性校验
- [x] 上游响应入口：添加包裹存在性校验
- [x] 所有关键事件：添加 Trace 日志（带时间戳）
- [x] 违反不变式：记录 Error 日志
- [x] SafeExecutor：确保异常不导致崩溃

### 5.2 测试层面

- [x] E2E 测试：严格时间顺序断言
- [x] E2E 测试：禁止"无包裹的上游请求"
- [x] E2E 测试：禁止"路由绑定到幽灵包裹"
- [x] E2E 测试：成功场景 Error 日志数 = 0
- [x] E2E 测试：Parcel Trace 完整性验证

### 5.3 文档层面

- [x] 规范文档：本文档（PR42_PARCEL_FIRST_SPECIFICATION.md）
- [x] 实施总结：PR42_IMPLEMENTATION_SUMMARY.md
- [x] 更新架构文档：说明 Parcel-First 语义

## 六、验收标准

### 6.1 功能验收

| 验收项 | 标准 | 验证方式 |
|--------|------|----------|
| 时间顺序不变式 | 严格单调递增 | E2E 测试断言时间戳 |
| 无包裹的上游请求 | 0 次 | Mock 收集所有请求并断言 |
| 路由绑定幽灵包裹 | 0 次 | Mock 收集所有绑定并断言 |
| 成功场景 Error 日志 | 0 条 | InMemoryLogCollector 断言 |
| Parcel Trace 完整性 | 100% | 验证所有事件存在且有序 |

### 6.2 覆盖率要求

| 模块 | 行覆盖率 | 分支覆盖率 |
|------|---------|-----------|
| 包裹创建逻辑 | ≥ 90% | 关键分支 100% |
| 上游请求构造 | ≥ 90% | 关键分支 100% |
| 上游响应处理 | ≥ 90% | 关键分支 100% |
| E2E 场景 | N/A | 所有场景通过 |

### 6.3 性能要求

- 包裹创建延迟：< 10ms
- 上游请求延迟：< 50ms
- 整体分拣延迟：< 系统配置的超时时间

## 七、禁止模式（Anti-Patterns）

### 7.1 ❌ 先向上游请求再创建包裹

```csharp
// ❌ 错误示例：先请求路由，再创建包裹
var routingInfo = await _upstream.RequestRouteAsync();
var parcel = CreateParcel(routingInfo);
```

**正确做法**：
```csharp
// ✅ 正确示例：先创建包裹，再请求路由
var parcel = CreateParcel(sensorEvent);
var routingInfo = await _upstream.RequestRouteAsync(parcel.Id);
```

### 7.2 ❌ 接受未知包裹的路由响应并创建包裹

```csharp
// ❌ 错误示例：收到路由后才创建包裹
void OnRouteReceived(ParcelId id, ChuteId chute)
{
    var parcel = _repo.Find(id);
    if (parcel == null)
    {
        parcel = new Parcel { Id = id }; // 创建幽灵包裹
        _repo.Add(parcel);
    }
    parcel.Route = chute;
}
```

**正确做法**：
```csharp
// ✅ 正确示例：找不到包裹就丢弃响应
void OnRouteReceived(ParcelId id, ChuteId chute)
{
    var parcel = _repo.Find(id);
    if (parcel == null)
    {
        _logger.LogError("收到未知包裹 {ParcelId} 的路由，响应已丢弃", id);
        return; // 不创建幽灵包裹
    }
    parcel.Route = chute;
}
```

### 7.3 ❌ 忽略时间戳顺序验证

```csharp
// ❌ 错误示例：不验证时间顺序
void BindRoute(Parcel parcel, Route route)
{
    parcel.Route = route;
    parcel.RouteBoundAt = DateTime.UtcNow;
}
```

**正确做法**：
```csharp
// ✅ 正确示例：验证时间顺序
void BindRoute(Parcel parcel, Route route)
{
    if (DateTime.UtcNow <= parcel.CreatedAt)
    {
        _logger.LogError("时间顺序异常：路由绑定时间早于包裹创建时间");
        return;
    }
    parcel.Route = route;
    parcel.RouteBoundAt = DateTime.UtcNow;
}
```

## 八、术语表

| 术语 | 定义 |
|------|------|
| Parcel-First | 本地包裹实体优先的架构语义 |
| 不变式 (Invariant) | 系统必须始终满足的约束条件 |
| 幽灵包裹 (Phantom Parcel) | 不存在本地实体的包裹ID引用 |
| 上游 (Upstream) | RuleEngine 或其他路由分配系统 |
| 本地 (Local) | 本分拣系统的包裹管理子系统 |
| 乱序 (Out-of-Order) | 违反时间顺序约束的事件序列 |
| 重放攻击 (Replay Attack) | 恶意或错误的重复消息 |

## 九、参考文档

- `PR41_E2E_SIMULATION_SUMMARY.md` - PR41 端到端测试基础
- `IMPLEMENTATION_SUMMARY_PUSH_MODEL.md` - 上游推送模型
- `PR10_PARCEL_TRACE_LOGGING.md` - 包裹追踪日志
- `ERROR_CORRECTION_MECHANISM.md` - 错误纠正机制

---

**版本**：v1.0  
**状态**：✅ 规范定义完成  
**实施日期**：2025-11-20
