# CI/CD 自动化测试配置说明

## 概述

本项目已配置完整的CI/CD自动化测试流程，使用GitHub Actions实现。每次代码推送或Pull Request都会自动运行所有测试并生成覆盖率报告。

## 配置文件

- **工作流文件**：`.github/workflows/dotnet.yml`
- **触发条件**：
  - Push到分支：`main`, `master`, `develop`
  - Pull Request到分支：`main`, `master`, `develop`

## 功能特性

### 1. 自动化构建和测试

每次代码变更时自动执行：

1. **环境准备**
   - 检出代码
   - 安装.NET 8.0 SDK

2. **构建流程**
   - 恢复依赖包 (`dotnet restore`)
   - 编译项目 (`dotnet build`)
   - 运行所有测试 (`dotnet test`)

3. **测试执行**
   - 运行所有单元测试、集成测试
   - 生成测试报告（TRX格式）
   - 收集代码覆盖率数据

### 2. 测试报告生成

#### XUnit测试报告
- 格式：TRX (Visual Studio测试结果格式)
- 存储位置：`TestResults/**/*.trx`
- 下载方式：GitHub Actions构建产物中的`test-results`

#### 覆盖率报告
生成三种格式的覆盖率报告：

1. **Cobertura XML**
   - 机器可读格式
   - 用于覆盖率门槛检查
   - 位置：`coveragereport/Cobertura.xml`

2. **HTML报告**
   - 人类可读的详细报告
   - 显示每个文件的覆盖率
   - 高亮未覆盖的代码行
   - 下载方式：GitHub Actions构建产物中的`coverage-report`

3. **Markdown摘要**
   - 用于PR评论
   - 显示整体覆盖率统计
   - 位置：`coveragereport/SummaryGithub.md`

### 3. 覆盖率门槛检查

实施两级覆盖率要求：

| 覆盖率范围 | 状态 | 说明 |
|-----------|------|------|
| < 60% | ❌ 失败 | 低于最低要求，构建失败 |
| 60% - 79% | ⚠️ 警告 | 满足最低要求但未达目标，构建通过 |
| ≥ 80% | ✅ 成功 | 达到目标，构建成功 |

**示例输出**：
```
📊 Code coverage: 65%
🎯 Target: 80%
🚪 Minimum threshold: 60%
⚠️ Coverage 65% meets minimum (60%) but below target (80%)
```

### 4. Pull Request集成

在Pull Request中自动添加覆盖率评论：

- 使用sticky comment（持久化评论）
- 每次更新会更新同一条评论
- 显示覆盖率统计和变化
- 包含详细的模块覆盖率信息

### 5. 构建产物

每次构建都会上传以下产物：

1. **test-results**：所有测试的TRX报告
2. **coverage-report**：完整的覆盖率报告（HTML + Cobertura + Markdown）

下载方式：GitHub Actions → 选择工作流运行 → Artifacts

## 使用指南

### 查看构建状态

1. **README徽章**：显示最新构建状态
   ```markdown
   [![.NET Build and Test](https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter/actions/workflows/dotnet.yml)
   ```

2. **Actions页面**：查看所有构建历史
   - 访问：`https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter/actions`

### 查看测试报告

#### 在线查看
1. 进入GitHub仓库的Actions页面
2. 选择一个工作流运行
3. 查看"Test"步骤的输出

#### 下载详细报告
1. 进入GitHub仓库的Actions页面
2. 选择一个工作流运行
3. 在页面底部找到"Artifacts"
4. 下载`test-results`或`coverage-report`
5. 解压后打开HTML文件查看

### 查看覆盖率报告

#### 在Pull Request中
- 覆盖率摘要会自动作为评论添加到PR中
- 包含：
  - 整体覆盖率百分比
  - 各模块的覆盖率
  - 覆盖/未覆盖的行数统计

#### 下载完整报告
1. 下载`coverage-report`构建产物
2. 解压后打开`index.html`
3. 浏览器中查看详细的覆盖率信息

## 本地运行

### 运行测试
```bash
dotnet test --configuration Release --verbosity normal
```

### 生成覆盖率报告
```bash
# 运行测试并收集覆盖率
dotnet test --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults

# 安装ReportGenerator工具（首次）
dotnet tool install -g dotnet-reportgenerator-globaltool

# 生成HTML报告
reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:"HtmlInline;Cobertura"

# 在浏览器中打开
open coveragereport/index.html  # macOS
xdg-open coveragereport/index.html  # Linux
start coveragereport/index.html  # Windows
```

### 检查覆盖率是否达标
```bash
# 提取覆盖率数值
coverage=$(grep -oP 'line-rate="\K[^"]+' coveragereport/Cobertura.xml | head -1)
coverage_percent=$(echo "$coverage * 100" | bc -l)
echo "当前覆盖率: ${coverage_percent}%"

# 检查是否满足60%最低要求
if (( $(echo "$coverage < 0.60" | bc -l) )); then
  echo "❌ 未达到最低要求60%"
else
  echo "✅ 满足最低要求"
fi
```

## 故障排查

### 构建失败

1. **编译错误**
   - 查看"Build"步骤的输出
   - 修复代码编译错误后重新提交

2. **测试失败**
   - 查看"Test"步骤的输出
   - 查找失败的测试和错误信息
   - 下载test-results查看详细报告

3. **覆盖率不达标**
   - 当前覆盖率：14.04%
   - 最低要求：60%
   - 建议：补充单元测试，特别是：
     - Communication层（0个测试）
     - Execution.Concurrency（0个测试）
     - Observability（0个测试）

### 报告未生成

如果覆盖率报告未生成：
1. 检查测试是否成功运行
2. 检查`TestResults`目录是否存在`coverage.cobertura.xml`
3. 查看"Generate coverage report"步骤的输出

### PR评论未添加

如果PR中没有看到覆盖率评论：
1. 确认workflow有`pull-requests: write`权限
2. 检查"Add coverage comment to PR"步骤是否运行
3. 检查是否生成了`coveragereport/SummaryGithub.md`

## 配置说明

### 修改覆盖率门槛

在`.github/workflows/dotnet.yml`中修改：

```yaml
# 修改最低要求（当前60%）
if (( $(echo "$coverage < 0.60" | bc -l) )); then
  # 改为0.50即50%

# 修改目标值（当前80%）
elif (( $(echo "$coverage < 0.80" | bc -l) )); then
  # 改为0.70即70%
```

### 添加其他触发分支

在`on`部分添加分支：

```yaml
on:
  push:
    branches: [ main, master, develop, feature/* ]  # 添加feature分支
  pull_request:
    branches: [ main, master, develop ]
```

### 修改.NET版本

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '9.0.x'  # 修改为9.0
```

## 最佳实践

### 提交前检查

在提交代码前建议：
1. 本地运行测试确保通过
2. 检查覆盖率是否满足要求
3. 修复所有编译警告

### 提高覆盖率

优先补充测试的模块（按优先级）：

1. **Communication层**（当前0个测试）
   - TCP/SignalR/MQTT/HTTP客户端测试
   - 预计新增50个测试

2. **Execution.Concurrency**（当前0个测试）
   - 并发控制器测试
   - 预计新增30个测试

3. **Observability**（当前0个测试）
   - 指标收集测试
   - 预计新增20个测试

4. **集成测试修复**
   - 修复当前2个失败的集成测试

### 保持构建绿色

- 不合并失败的PR
- 及时修复失败的测试
- 定期更新依赖包
- 监控覆盖率趋势

## 相关文档

- [测试文档](TESTING.md)
- [README - 风险与缺陷](README.md#-当前风险与缺陷)
- [优化路线图](README.md#-未来优化方向)

## 更新历史

- **2025-11-14**：初始配置，修复工作流语法错误，配置覆盖率门槛检查
- 下次更新：根据实际使用情况调整配置

---

**配置版本**：v1.0  
**最后更新**：2025-11-14
