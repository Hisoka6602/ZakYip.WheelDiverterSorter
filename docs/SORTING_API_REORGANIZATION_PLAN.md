# 分拣API重组与新功能实施计划

**文档版本**: 1.0  
**创建日期**: 2025-12-13  
**作者**: GitHub Copilot  
**状态**: 实施中

---

## 一、需求概述

### 1.1 总体目标

将分散的分拣相关API端点整合到统一的`SortingController`中，并新增3个功能端点。

### 1.2 具体需求

**需求1：API控制器重组**
- 将`DivertsController`的分拣改口功能迁移到`SortingController`
- 将`SimulationController`的分拣测试功能迁移到`SortingController`
- 统一路由前缀为`/api/sorting`

**需求2：新增功能端点**
1. **Position间隔查询端点**
   - 路径：`GET /api/sorting/position-intervals`
   - 功能：查询各positionIndex的实际触发间隔中位数
   - 返回：所有position的统计信息

2. **落格回调配置设置端点**
   - 路径：`POST /api/sorting/chute-dropoff-callback-config`
   - 功能：配置落格回调触发模式
   - 模式选项：
     - `OnWheelExecution`：执行摆轮动作时发送
     - `OnSensorTrigger`：落格传感器感应时发送

3. **落格回调配置查询端点**
   - 路径：`GET /api/sorting/chute-dropoff-callback-config`
   - 功能：查询当前的落格回调配置

**需求3：枚举扩展**
- 在`SensorIoType`枚举中新增`ChuteDropoff`值
- 描述：落格传感器，用于检测包裹落入格口

---

## 二、技术设计

### 2.1 新增枚举值

**文件**: `src/Core/ZakYip.WheelDiverterSorter.Core/Enums/Hardware/SensorIoType.cs`

```csharp
/// <summary>
/// 感应IO类型 - 按业务功能分类
/// </summary>
public enum SensorIoType
{
    /// <summary>
    /// 创建包裹感应IO
    /// </summary>
    [Description("创建包裹感应IO")]
    ParcelCreation = 0,

    /// <summary>
    /// 摆轮前感应IO
    /// </summary>
    [Description("摆轮前感应IO")]
    WheelFront = 1,

    /// <summary>
    /// 锁格感应IO
    /// </summary>
    [Description("锁格感应IO")]
    ChuteLock = 2,

    /// <summary>
    /// 落格感应IO - 检测包裹落入格口
    /// </summary>
    /// <remarks>
    /// 用于在包裹落入格口时触发落格回调，向上游发送分拣完成通知
    /// </remarks>
    [Description("落格感应IO")]
    ChuteDropoff = 3  // 新增
}
```

### 2.2 新增配置模型

**文件**: `src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/Models/ChuteDropoffCallbackConfiguration.cs`

```csharp
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 落格回调配置模型
/// </summary>
/// <remarks>
/// 定义落格回调的触发模式，决定何时向上游发送分拣完成通知
/// </remarks>
public record ChuteDropoffCallbackConfiguration
{
    /// <summary>
    /// 配置ID（数据库主键）
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 配置名称
    /// </summary>
    public required string ConfigName { get; init; }

    /// <summary>
    /// 落格回调触发模式
    /// </summary>
    public ChuteDropoffCallbackMode TriggerMode { get; init; }

    /// <summary>
    /// 是否启用落格回调
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 版本号（用于并发控制）
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// 获取默认配置
    /// </summary>
    public static ChuteDropoffCallbackConfiguration GetDefault()
    {
        var now = ConfigurationDefaults.DefaultTimestamp;
        return new ChuteDropoffCallbackConfiguration
        {
            Id = 1,
            ConfigName = "chute-dropoff-callback",
            TriggerMode = ChuteDropoffCallbackMode.OnSensorTrigger,  // 默认使用传感器触发
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
    }
}
```

### 2.3 新增枚举：落格回调模式

**文件**: `src/Core/ZakYip.WheelDiverterSorter.Core/Enums/Sorting/ChuteDropoffCallbackMode.cs`

```csharp
using System.ComponentModel;

namespace ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

/// <summary>
/// 落格回调触发模式
/// </summary>
/// <remarks>
/// 定义向上游发送分拣完成通知的触发时机
/// </remarks>
public enum ChuteDropoffCallbackMode
{
    /// <summary>
    /// 执行摆轮动作时触发
    /// </summary>
    /// <remarks>
    /// 在摆轮执行导向动作后立即发送分拣完成通知，
    /// 不等待包裹实际落入格口
    /// </remarks>
    [Description("执行摆轮动作时触发")]
    OnWheelExecution = 0,

    /// <summary>
    /// 落格传感器触发时触发
    /// </summary>
    /// <remarks>
    /// 等待落格传感器（ChuteDropoff）检测到包裹落入格口后，
    /// 再发送分拣完成通知
    /// </remarks>
    [Description("落格传感器触发时触发")]
    OnSensorTrigger = 1
}
```

### 2.4 新增DTO模型

**文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/Models/PositionIntervalDto.cs`

```csharp
namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// Position间隔统计DTO
/// </summary>
public record PositionIntervalDto
{
    /// <summary>
    /// Position索引
    /// </summary>
    public int PositionIndex { get; init; }

    /// <summary>
    /// 触发间隔中位数（毫秒）
    /// </summary>
    public double? MedianIntervalMs { get; init; }

    /// <summary>
    /// 样本数量
    /// </summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// 最小间隔（毫秒）
    /// </summary>
    public double? MinIntervalMs { get; init; }

    /// <summary>
    /// 最大间隔（毫秒）
    /// </summary>
    public double? MaxIntervalMs { get; init; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? LastUpdatedAt { get; init; }
}
```

**文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/Models/ChuteDropoffCallbackConfigDto.cs`

```csharp
namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 落格回调配置DTO
/// </summary>
public record ChuteDropoffCallbackConfigDto
{
    /// <summary>
    /// 触发模式
    /// </summary>
    /// <example>OnSensorTrigger</example>
    public required string TriggerMode { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
```

**文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/Models/UpdateChuteDropoffCallbackConfigRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models;

/// <summary>
/// 更新落格回调配置请求
/// </summary>
public record UpdateChuteDropoffCallbackConfigRequest
{
    /// <summary>
    /// 触发模式
    /// </summary>
    /// <example>OnSensorTrigger</example>
    [Required(ErrorMessage = "TriggerMode is required")]
    public required string TriggerMode { get; init; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}
```

### 2.5 SortingController设计

**文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/Controllers/SortingController.cs`

**路由结构**:
```
/api/sorting
├── POST   /change-chute                      # 分拣改口
├── POST   /test                              # 分拣测试
├── GET    /position-intervals                # Position间隔查询
├── GET    /chute-dropoff-callback-config     # 落格回调配置查询
└── POST   /chute-dropoff-callback-config     # 落格回调配置设置
```

---

## 三、实施步骤

### Phase 1: Core层扩展（15分钟）

1. ✅ 新增`SensorIoType.ChuteDropoff`枚举值
2. ✅ 新增`ChuteDropoffCallbackMode`枚举
3. ✅ 新增`ChuteDropoffCallbackConfiguration`配置模型
4. ✅ 在`ConfigurationDefaults`中添加默认值支持

### Phase 2: Host层DTO模型（10分钟）

1. ✅ 创建`PositionIntervalDto`
2. ✅ 创建`ChuteDropoffCallbackConfigDto`
3. ✅ 创建`UpdateChuteDropoffCallbackConfigRequest`

### Phase 3: 创建SortingController（30分钟）

1. ✅ 创建`SortingController`基础结构
2. ✅ 迁移`change-chute`端点（从DivertsController）
3. ✅ 迁移`test`端点（从SimulationController的`/sort`）
4. ✅ 实现`GET /position-intervals`端点
5. ✅ 实现`GET /chute-dropoff-callback-config`端点
6. ✅ 实现`POST /chute-dropoff-callback-config`端点

### Phase 4: 更新依赖服务（20分钟）

1. ✅ 创建或扩展Position间隔追踪服务接口
2. ✅ 创建或扩展落格回调配置服务接口
3. ✅ 实现配置读写逻辑

### Phase 5: 测试验证（15分钟）

1. ✅ 编译验证
2. ✅ API端点可访问性测试
3. ✅ Swagger文档生成验证

### Phase 6: 清理与文档（10分钟）

1. ✅ 标记旧端点为Obsolete（如需保持向后兼容）
2. ✅ 或删除旧端点（如不需要向后兼容）
3. ✅ 更新API文档

**总预计时间**: 100分钟（约1.5-2小时）

---

## 四、向后兼容性

### 4.1 方案选择

**选项A（推荐）**: 保留旧端点并标记为Obsolete
- 优点：平滑迁移，给客户端时间适配
- 缺点：维护两套端点

**选项B**: 直接删除旧端点
- 优点：代码简洁
- 缺点：破坏现有客户端

**决策**: 采用选项A，保留旧端点3个月后再删除

### 4.2 路由映射

**旧路由** → **新路由**

| 旧路由 | 新路由 | 状态 |
|--------|--------|------|
| `POST /api/diverts/change-chute` | `POST /api/sorting/change-chute` | Obsolete |
| `POST /api/simulation/sort` | `POST /api/sorting/test` | Obsolete |

---

## 五、测试计划

### 5.1 单元测试

- [ ] `SortingController`各端点的基本功能测试
- [ ] DTO模型验证测试
- [ ] 枚举值有效性测试

### 5.2 集成测试

- [ ] Position间隔追踪完整流程测试
- [ ] 落格回调配置读写测试
- [ ] 分拣改口端点迁移后功能测试

### 5.3 E2E测试

- [ ] 通过Swagger测试所有新端点
- [ ] 验证旧端点仍然可用（向后兼容）

---

## 六、风险与缓解措施

### 6.1 已识别风险

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| 破坏现有客户端 | 高 | 中 | 保留旧端点并标记Obsolete |
| Position间隔服务未实现 | 中 | 中 | 如未实现则返回占位数据 |
| 落格回调配置冲突 | 低 | 低 | 使用版本号控制并发 |

### 6.2 回滚计划

如果发现严重问题：
1. 恢复`DivertsController`和`SimulationController`的原始端点
2. 删除`SortingController`
3. 回滚数据库迁移（如有）

---

## 七、交付物检查清单

- [ ] `SensorIoType`枚举扩展
- [ ] `ChuteDropoffCallbackMode`枚举
- [ ] `ChuteDropoffCallbackConfiguration`配置模型
- [ ] DTO模型（3个）
- [ ] `SortingController`（5个端点）
- [ ] Position间隔追踪服务
- [ ] 落格回调配置服务
- [ ] Swagger文档注释完整
- [ ] 编译通过
- [ ] 基础测试通过

---

## 八、后续工作

1. 实施Phase 1（方案A+）的包裹丢失检测功能
2. 集成Position间隔追踪到包裹丢失检测
3. 实施落格回调触发逻辑
4. 3个月后删除Obsolete的旧端点

---

**文档结束**
