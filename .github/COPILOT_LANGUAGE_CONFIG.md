# GitHub Copilot 配置

## Overview 显示语言设置

本项目的 GitHub Copilot Overview 和任务描述统一使用**中文**。

### 配置说明

1. **PR描述语言**: 中文
2. **Commit消息**: 中文或中英文混合
3. **代码注释**: 中文（XML文档注释）
4. **文档**: 中文
5. **Issue/Task描述**: 中文

### Copilot Tasks 语言规范

所有通过 GitHub Copilot 创建的 Tasks 和 PR，其 Overview 部分必须使用中文描述，包括：

- 任务目标
- 实现计划
- 进度追踪
- 测试验证
- 完成总结

### 示例

#### ✅ 正确的Overview格式
```markdown
## 任务概述

本PR实现TCP Keep-Alive功能，解决TCP频繁断线问题。

### 主要工作
- 实现跨平台TCP Keep-Alive
- 添加配置选项
- 补充完整测试
```

#### ❌ 错误的Overview格式
```markdown
## Task Overview

This PR implements TCP Keep-Alive functionality.

### Main Work
- Implement cross-platform TCP Keep-Alive
- Add configuration options
- Add comprehensive tests
```

### 技术文档例外

以下情况可以使用英文：
- 代码标识符（变量名、方法名等）
- 技术术语（如TCP、Keep-Alive、Socket等）
- 第三方库和API名称
- 日志消息中的技术细节

但即使在这些情况下，说明性文字也应使用中文。

## 相关文件

- `.github/PULL_REQUEST_TEMPLATE.md` - PR模板（中文）
- `.github/copilot-instructions.md` - Copilot指令（中文）
- `docs/` - 项目文档（中文）

## 强制执行

所有PR在Code Review阶段会检查Overview语言是否符合规范。不符合规范的PR将被要求修改后重新提交。
