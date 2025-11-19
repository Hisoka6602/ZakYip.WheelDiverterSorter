# 分拣模式说明文档 (Sorting Modes Documentation)

## 概述 (Overview)

摆轮分拣系统支持三种分拣模式，用于满足不同的业务场景和测试需求。系统默认使用**正式分拣模式（Formal）**，可通过API动态切换。

## 三种分拣模式 (Three Sorting Modes)

### 1. 正式分拣模式 (Formal Mode) - **默认模式**

**用途**：正式生产环境使用

**工作原理**：
- 系统通过包裹检测传感器识别包裹到达
- 向上游 RuleEngine 发送包裹检测通知
- RuleEngine 根据业务规则（订单信息、库存、路由规则等）计算并返回目标格口
- 系统接收格口分配结果，生成摆轮路径并执行分拣

**适用场景**：
- 正式生产环境
- 与上游WMS/RuleEngine集成的完整分拣系统
- 需要根据实际业务规则动态分配格口的场景

**配置示例**：
```json
{
  "sortingMode": "Formal"
}
```

**API端点**：
```
PUT /api/config/system
PUT /api/config/sorting-mode
```

**特点**：
- ✅ 需要上游RuleEngine连接
- ✅ 支持复杂业务规则
- ✅ 实时格口分配
- ✅ 异常处理完善（超时、失败自动走异常格口）

---

### 2. 指定落格模式 (FixedChute Mode)

**用途**：单格口测试、设备调试

**工作原理**：
- 所有包裹统一分拣到预先配置的固定格口
- 不依赖上游RuleEngine，独立运行
- 异常包裹仍走异常格口

**适用场景**：
- 单个格口的设备调试和性能测试
- 不需要连接上游系统的独立测试
- 验证特定格口的摆轮路径和分拣精度
- 现场安装调试阶段

**配置示例**：
```json
{
  "sortingMode": "FixedChute",
  "fixedChuteId": 5
}
```

**必需参数**：
- `fixedChuteId`: 固定格口ID（必须大于0且存在于路由配置中）

**API端点**：
```
PUT /api/config/system
PUT /api/config/sorting-mode
```

**特点**：
- ✅ 无需上游连接
- ✅ 配置简单
- ✅ 独立运行
- ✅ 适合单点调试
- ⚠️ 所有包裹只能到一个格口

---

### 3. 循环落格模式 (RoundRobin Mode)

**用途**：多格口均衡测试、压力测试

**工作原理**：
- 包裹依次循环分配到可用格口列表中的格口
- 第1个包裹 → 格口1，第2个包裹 → 格口2，...，第N+1个包裹 → 格口1
- 不依赖上游RuleEngine，按固定顺序循环
- 异常包裹仍走异常格口

**适用场景**：
- 多格口性能测试和压力测试
- 验证所有格口的摆轮路径正确性
- 不需要业务规则的简单分拣场景
- 系统产能测试和瓶颈分析

**配置示例**：
```json
{
  "sortingMode": "RoundRobin",
  "availableChuteIds": [1, 2, 3, 4, 5, 6, 7, 8]
}
```

**必需参数**：
- `availableChuteIds`: 可用格口ID列表（数组）
  - 至少包含1个格口ID
  - 所有格口ID必须存在于路由配置中
  - 格口ID必须大于0

**API端点**：
```
PUT /api/config/system
PUT /api/config/sorting-mode
```

**特点**：
- ✅ 无需上游连接
- ✅ 多格口均衡分配
- ✅ 适合压力测试
- ✅ 配置灵活
- ⚠️ 不考虑业务规则，纯循环分配

---

## 模式切换 (Switching Modes)

### 使用配置API切换模式

**端点1：通过系统配置API**
```http
PUT /api/config/system
Content-Type: application/json

{
  "exceptionChuteId": 999,
  "sortingMode": "RoundRobin",
  "availableChuteIds": [1, 2, 3, 4, 5]
}
```

**端点2：通过专用分拣模式API**
```http
PUT /api/config/sorting-mode
Content-Type: application/json

{
  "sortingMode": "FixedChute",
  "fixedChuteId": 5
}
```

### 查询当前模式

```http
GET /api/config/sorting-mode
```

响应示例：
```json
{
  "sortingMode": "Formal",
  "fixedChuteId": null,
  "availableChuteIds": []
}
```

---

## 异常处理 (Exception Handling)

**所有模式共享的异常格口机制**：

无论使用哪种分拣模式，以下情况包裹会被分拣到异常格口：

1. **正式模式（Formal）**：
   - 上游RuleEngine超时未响应
   - 上游RuleEngine返回无效格口ID
   - 通信连接失败
   - 格口分配重试失败

2. **指定落格模式（FixedChute）**：
   - 配置的固定格口路径不可达
   - 路径生成失败

3. **循环落格模式（RoundRobin）**：
   - 所有可用格口路径都不可达
   - 路径生成失败

4. **通用异常情况**：
   - 拓扑配置错误导致无法生成路径
   - TTL（Time-To-Live）超时
   - 驱动器故障

**异常格口配置**：
```json
{
  "exceptionChuteId": 999
}
```

---

## 配置验证规则 (Validation Rules)

### Formal模式验证
- ✅ 无特殊参数要求
- ✅ 建议确保上游RuleEngine连接正常

### FixedChute模式验证
- ✅ `fixedChuteId` 必须提供
- ✅ `fixedChuteId` 必须大于0
- ✅ `fixedChuteId` 对应的路由配置必须存在且已启用

### RoundRobin模式验证
- ✅ `availableChuteIds` 必须提供
- ✅ `availableChuteIds` 至少包含1个格口ID
- ✅ 所有格口ID必须大于0
- ✅ 所有格口ID对应的路由配置必须存在且已启用

---

## 最佳实践 (Best Practices)

### 1. 生产环境
- 使用 **Formal** 模式
- 配置上游RuleEngine连接
- 设置合理的超时和重试参数
- 配置告警监控

### 2. 测试环境
- 单格口调试：使用 **FixedChute** 模式
- 多格口测试：使用 **RoundRobin** 模式
- 性能测试：使用 **RoundRobin** 模式 + 高密度包裹流

### 3. 调试建议
- 模式切换立即生效，无需重启
- 使用 `/api/config/sorting-mode` 查询当前模式
- 切换模式前确保相关格口路由配置已就绪
- 查看日志确认模式切换成功

### 4. 监控要点
- 监控当前分拣模式
- 监控异常格口使用率
- Formal模式：监控上游连接状态和响应时间
- RoundRobin模式：监控格口分配均衡性

---

## 相关API文档 (Related API Documentation)

详细的API使用说明请参考：
- Swagger UI: `/swagger`
- 配置管理API: `/api/config/*`
- 系统状态API: `/health/line`

---

## 枚举定义 (Enum Definition)

**C# 枚举定义**：
```csharp
namespace ZakYip.WheelDiverterSorter.Core.Sorting.Models;

/// <summary>
/// 分拣模式枚举
/// </summary>
public enum SortingMode
{
    /// <summary>
    /// 正式分拣模式（默认）
    /// </summary>
    /// <remarks>
    /// 由上游 Sorting.RuleEngine 给出格口分配
    /// </remarks>
    Formal = 0,

    /// <summary>
    /// 指定落格分拣模式
    /// </summary>
    /// <remarks>
    /// 可设置固定格口落格（异常除外），每次都只在指定的格口ID落格
    /// </remarks>
    FixedChute = 1,

    /// <summary>
    /// 循环格口落格模式
    /// </summary>
    /// <remarks>
    /// 第一个包裹落格口1，第二个包裹落格口2，以此类推循环分配
    /// </remarks>
    RoundRobin = 2
}
```

---

## 版本历史 (Version History)

- **v1.0** (PR-30): 初始版本，文档化三种分拣模式
- 枚举定义位于 `ZakYip.WheelDiverterSorter.Core/Sorting/Models/SortingMode.cs`
- API端点位于 `ConfigurationController` 和 `SystemConfigController`

---

## 联系方式 (Contact)

如有问题或建议，请联系技术支持团队。
