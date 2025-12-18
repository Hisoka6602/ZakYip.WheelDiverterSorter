# Windows Service 启动失败诊断增强说明

## 概述

本文档说明为解决 Windows Service 错误 1053（服务没有及时响应启动或控制请求）而进行的改进。

## 问题描述

Windows Service 启动失败时，原有的日志记录不够详细，无法快速定位问题根源。特别是：
1. 缺少启动过程的详细步骤记录
2. 异常信息不完整，缺少内部异常和堆栈跟踪
3. 无法识别启动超时的具体原因
4. NLog 自身的问题难以诊断

## 解决方案

### 1. 增强启动日志记录

**文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/Program.cs`

**改进内容**:
- 记录启动开始时的关键环境信息：
  - 应用程序版本
  - 工作目录
  - 进程 ID
  - .NET 版本
  - 操作系统
  - 是否 64 位进程
  - 是否自包含部署
- 在每个关键步骤添加日志记录点：
  - WebApplication Builder 构建
  - 配置加载
  - 服务注册
  - 中间件配置
  - 应用程序启动完成

**示例日志**:
```
2025-12-18 11:13:26|INFO|ZakYip.WheelDiverterSorter.Host.Program|========== 应用程序启动开始 ==========
2025-12-18 11:13:26|INFO|ZakYip.WheelDiverterSorter.Host.Program|应用程序版本: 1.0.0.0
2025-12-18 11:13:26|INFO|ZakYip.WheelDiverterSorter.Host.Program|工作目录: C:\Program Files\WheelDiverterSorter
2025-12-18 11:13:26|INFO|ZakYip.WheelDiverterSorter.Host.Program|进程 ID: 12345
2025-12-18 11:13:26|INFO|ZakYip.WheelDiverterSorter.Host.Program|.NET 版本: 8.0.0
2025-12-18 11:13:26|INFO|ZakYip.WheelDiverterSorter.Host.Program|操作系统: Microsoft Windows NT 10.0.19045.0
2025-12-18 11:13:26|INFO|ZakYip.WheelDiverterSorter.Host.Program|是否 64 位进程: True
2025-12-18 11:13:26|INFO|ZakYip.WheelDiverterSorter.Host.Program|是否自包含部署: True
2025-12-18 11:13:26|INFO|ZakYip.WheelDiverterSorter.Host.Program|=========================================
```

### 2. 增强异常日志记录

**改进内容**:
- 捕获所有顶层异常
- 记录完整的异常信息：
  - 异常类型
  - 异常消息
  - 完整堆栈跟踪
  - 所有内部异常（递归遍历）
- 异常发生时强制刷新日志到磁盘（避免日志丢失）

**示例异常日志**:
```
2025-12-18 11:13:26|FATAL|ZakYip.WheelDiverterSorter.Host.Program|========== 应用程序启动失败 ==========
2025-12-18 11:13:26|FATAL|ZakYip.WheelDiverterSorter.Host.Program|异常类型: System.IO.FileNotFoundException
2025-12-18 11:13:26|FATAL|ZakYip.WheelDiverterSorter.Host.Program|异常消息: Could not load file or assembly 'LTDMC.dll'
2025-12-18 11:13:26|FATAL|ZakYip.WheelDiverterSorter.Host.Program|堆栈跟踪:
   at ZakYip.WheelDiverterSorter.Drivers.Leadshine.EmcController..ctor()
   at Program.<Main>$(String[] args) in Program.cs:line 56
2025-12-18 11:13:26|FATAL|ZakYip.WheelDiverterSorter.Host.Program|内部异常 #1 类型: System.DllNotFoundException
2025-12-18 11:13:26|FATAL|ZakYip.WheelDiverterSorter.Host.Program|内部异常 #1 消息: Unable to load DLL 'LTDMC.dll'
2025-12-18 11:13:26|FATAL|ZakYip.WheelDiverterSorter.Host.Program|内部异常 #1 堆栈跟踪:
   at Leadshine.EmcNative.Initialize()
2025-12-18 11:13:26|FATAL|ZakYip.WheelDiverterSorter.Host.Program|=========================================
```

### 3. 添加 NLog 内部日志

**文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/nlog.config`

**改进内容**:
- 启用 NLog 内部日志记录（internalLogLevel="Warn"）
- 内部日志写入独立文件：`logs/internal-nlog-{date}.log`
- 内部日志同时输出到控制台（便于调试）
- 启用 throwConfigExceptions（配置错误时抛出异常）

**用途**:
- 诊断 NLog 配置错误
- 诊断日志文件权限问题
- 诊断日志目录访问问题

### 4. 新增启动日志文件

**文件**: `logs/startup-{date}.log`

**内容**:
- 专门记录应用程序启动过程的日志
- 包含 Program.cs 中的所有启动步骤
- 便于快速定位启动失败的具体步骤

### 5. 添加启动超时监控

**文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/Services/Workers/StartupMonitorHostedService.cs`

**功能**:
- 自动监控应用程序启动时间
- 超过 25 秒时发出警告（Windows Service 默认超时 30 秒）
- 提供详细的优化建议

**示例警告日志**:
```
2025-12-18 11:13:51|INFO|StartupMonitorHostedService|========== 启动监控报告 ==========
2025-12-18 11:13:51|INFO|StartupMonitorHostedService|应用程序启动耗时: 28500 ms (28.50 秒)
2025-12-18 11:13:51|WARN|StartupMonitorHostedService|⚠️ 应用程序启动耗时过长: 28500 ms，超过警告阈值 25000 ms
2025-12-18 11:13:51|WARN|StartupMonitorHostedService|这可能导致 Windows Service 启动超时（错误 1053）。请检查以下可能的原因：
2025-12-18 11:13:51|WARN|StartupMonitorHostedService|  1. 硬件设备初始化耗时过长（IO 板卡、PLC 等）
2025-12-18 11:13:51|WARN|StartupMonitorHostedService|  2. 数据库连接或查询耗时过长
2025-12-18 11:13:51|WARN|StartupMonitorHostedService|  3. 网络连接检查耗时过长（上游系统、设备等）
2025-12-18 11:13:51|WARN|StartupMonitorHostedService|  4. 配置文件加载或验证耗时过长
2025-12-18 11:13:51|WARN|StartupMonitorHostedService|  5. 大量后台服务同时启动
2025-12-18 11:13:51|WARN|StartupMonitorHostedService|建议: 考虑异步初始化非关键组件，或增加 Windows Service 启动超时时间。
2025-12-18 11:13:51|INFO|StartupMonitorHostedService|====================================
```

### 6. 增强自包含部署配置

**文件**: `src/Host/ZakYip.WheelDiverterSorter.Host/ZakYip.WheelDiverterSorter.Host.csproj`

**改进内容**:
- 添加 `IncludeNativeLibrariesForSelfExtract=true`（确保原生 DLL 正确提取）
- 添加 `CopyLocalLockFileAssemblies=true`（确保所有依赖文件复制）

**作用**:
- 确保厂商驱动 DLL（如 LTDMC.dll）正确打包
- 确保应用程序不依赖系统安装的 .NET Runtime

### 7. 更新故障排查文档

**文件**: `docs/WINDOWS_SERVICE_DEPLOYMENT.md`

**新增内容**:
- 详细的错误 1053 排查步骤（8 个步骤）
- 启动日志文件说明
- 启动超时诊断和解决方案
- 自包含部署验证步骤
- 错误 1053 专项排查清单（10 步检查）
- 常见错误对照表

## 使用指南

### 诊断启动失败的步骤

1. **查看启动日志** (最重要)
   ```powershell
   Get-Content "C:\Program Files\WheelDiverterSorter\Logs\startup-$(Get-Date -Format yyyy-MM-dd).log"
   ```
   确认启动到哪一步失败。

2. **查看错误日志**
   ```powershell
   Get-Content "C:\Program Files\WheelDiverterSorter\Logs\error-$(Get-Date -Format yyyy-MM-dd).log"
   ```
   查看完整的异常堆栈跟踪。

3. **查看 NLog 内部日志**
   ```powershell
   Get-Content "C:\Program Files\WheelDiverterSorter\Logs\internal-nlog-$(Get-Date -Format yyyy-MM-dd).log"
   ```
   诊断日志系统本身的问题。

4. **检查启动时间**
   查看日志中的"启动监控报告"，确认启动是否超时。

5. **验证自包含部署**
   ```powershell
   Get-ChildItem "C:\Program Files\WheelDiverterSorter" -Filter "System.*.dll" | Select-Object -First 5
   ```
   应该看到大量 System.*.dll 文件（自包含部署的标志）。

### 常见问题解决

| 问题 | 日志文件 | 解决方案 |
|------|---------|---------|
| 服务启动超时 | startup-*.log | 查看启动监控报告，增加服务超时或优化慢速组件 |
| 缺少 DLL | error-*.log | 检查异常消息，重新发布或复制缺失的文件 |
| 日志文件未生成 | internal-nlog-*.log | 检查日志目录权限 |
| 配置加载失败 | startup-*.log | 检查配置文件格式和路径 |
| 硬件初始化失败 | driver-*.log | 检查硬件连接和驱动安装 |

## 技术细节

### 日志文件结构

```
Logs/
├── startup-{date}.log       # 启动过程日志
├── error-{date}.log         # 错误和异常日志
├── internal-nlog-{date}.log # NLog 内部诊断日志
├── all-{date}.log           # 所有日志的完整备份
├── parcel-lifecycle-{date}.log
├── communication-{date}.log
└── ...（其他业务日志）
```

### 启动监控阈值

- **警告阈值**: 25 秒
- **Windows Service 默认超时**: 30 秒
- **建议**: 如果启动时间接近 25 秒，考虑优化或增加超时

### 异常记录深度

- **最大内部异常层级**: 无限制（递归遍历所有内部异常）
- **堆栈跟踪**: 完整记录所有层级
- **数据**: 包含异常的 Data 属性

## 相关文档

- [WINDOWS_SERVICE_DEPLOYMENT.md](./WINDOWS_SERVICE_DEPLOYMENT.md) - Windows Service 部署指南
- [PRODUCTION_SERVICE_STARTUP.md](./PRODUCTION_SERVICE_STARTUP.md) - 生产环境服务启动说明
- [SELF_CONTAINED_DEPLOYMENT.md](../SELF_CONTAINED_DEPLOYMENT.md) - 自包含部署说明

---

**维护团队**: ZakYip Development Team  
**最后更新**: 2025-12-18
