## PR 描述

<!-- 请简要描述此 PR 的目的和主要变更 -->

## 变更类型

<!-- 请勾选适用的选项 -->

- [ ] 新功能 (New Feature)
- [ ] Bug 修复 (Bug Fix)
- [ ] 性能优化 (Performance Optimization)
- [ ] 重构 (Refactoring)
- [ ] 文档更新 (Documentation)
- [ ] 测试增强 (Test Enhancement)
- [ ] 依赖更新 (Dependency Update)
- [ ] CI/CD 配置 (CI/CD Configuration)

## 变更检查清单（必须逐项确认）

### 代码质量基础

- [ ] 未在业务代码中直接使用 `DateTime.Now` 或 `DateTime.UtcNow`，所有时间通过 `ISystemClock` 获取
- [ ] 所有新增加或修改的后台任务/循环/IO/通讯回调均通过 `SafeExecutionService` 进行异常隔离
- [ ] 所有新建或修改的跨线程共享集合均使用线程安全容器（`ConcurrentDictionary`/`ConcurrentQueue`/`Immutable` 集合等）或明确的锁封装
- [ ] 启用可空引用类型（`Nullable=enable`），未新增 `#nullable disable` 指令

### API 端点规范（如适用）

- [ ] 所有新建 API 端点：
  - [ ] 使用 DTO（`record`）作为请求模型，并启用可空引用类型
  - [ ] 使用 `required + init` 标记必填字段
  - [ ] 已增加参数验证特性（如 `[Required]`/`[Range]`/`[StringLength]` 等）
  - [ ] 响应返回类型统一使用 `ApiResponse<T>`

### 架构与业务规则

- [ ] 未破坏 "先创建包裹（本地）再向上游请求路由" 的 **Parcel-First 分拣流程**
- [ ] Host 层未包含业务逻辑，所有业务规则在 `Core` / `Application` / `Execution` 层实现
- [ ] 硬件相关操作通过 `Drivers` 层接口访问，未直接依赖具体实现

### 路由 / 拓扑 分层检查（PR-9）

- [ ] 本次改动未在 `Routing` 命名空间中使用任何 `Topology` 类型或概念（线体长度、线体段、摆轮物理位置等）
- [ ] 本次改动未在 `Topology` 命名空间中加入任何业务路由规则（客户、条码前缀、业务策略等）
- [ ] 如需同时使用 `Routing` 和 `Topology`，相关代码已确认位于 `Orchestration` 命名空间或 `Application.Services` 层
- [ ] 架构测试（`ZakYip.WheelDiverterSorter.ArchTests`）已通过

### 代码风格

- [ ] DTO / 只读数据使用 `record` / `record struct`
- [ ] 小型值类型（≤16 字节）使用 `readonly struct`
- [ ] 工具类和内部辅助类使用 `file` 作用域类型（`file class` / `file struct`）
- [ ] 方法保持单一职责，长度不超过 30 行（复杂逻辑已拆分为多个小方法）

### 测试与质量保证

- [ ] 仿真和 E2E 测试已运行并通过，特别是从"API 配置启动 IO → 面板启动 → 创建包裹 → 上游路由 → 摆轮分拣 → 落格"的闭环场景
- [ ] 未注释或删除现有测试用例来绕过规则检查
- [ ] 新增功能已补充相应的单元测试和集成测试
- [ ] 构建成功，无编译错误和警告（`dotnet build` 通过）

## 测试验证

<!-- 请描述如何验证此 PR 的变更 -->

### 测试步骤

1. 
2. 
3. 

### 测试环境

- [ ] 本地开发环境
- [ ] 仿真环境
- [ ] 集成测试环境
- [ ] 生产环境（需特别批准）

### 测试结果

<!-- 请粘贴测试输出或截图 -->

```
# dotnet test 输出
```

## 相关文档更新

<!-- 如果变更影响到文档，请列出更新的文档 -->

- [ ] README.md
- [ ] API 文档
- [ ] 配置指南
- [ ] 架构文档
- [ ] 无需文档更新

## 依赖变更

<!-- 如果有新增、删除或更新依赖包，请列出 -->

- [ ] 无依赖变更
- [ ] 新增依赖：
- [ ] 删除依赖：
- [ ] 更新依赖：

## 安全检查（如适用）

- [ ] 未引入新的安全漏洞
- [ ] 未暴露敏感信息（密钥、密码、令牌等）
- [ ] 已通过 CodeQL 安全扫描（CI 自动执行）

## 性能影响（如适用）

<!-- 如果变更可能影响性能，请说明 -->

- [ ] 无性能影响
- [ ] 性能提升：
- [ ] 性能下降（需说明原因和接受理由）：

## 破坏性变更

- [ ] 此 PR 不包含破坏性变更
- [ ] 此 PR 包含破坏性变更（需在描述中详细说明迁移路径）

## Reviewer 检查清单

<!-- 仅供 Reviewer 使用 -->

- [ ] 代码符合编码规范（参考 [copilot-instructions.md](.github/copilot-instructions.md)）
- [ ] 架构设计合理，未违反分层原则
- [ ] 测试覆盖充分
- [ ] 文档准确且完整
- [ ] 无明显性能问题
- [ ] 无安全漏洞

## 其他说明

<!-- 任何其他需要说明的内容 -->
