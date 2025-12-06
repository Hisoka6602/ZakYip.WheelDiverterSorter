# 自包含部署说明 | Self-Contained Deployment Guide

本文档说明如何构建和部署不需要预安装 .NET Runtime 的自包含应用程序。

This document explains how to build and deploy a self-contained application that doesn't require pre-installed .NET Runtime.

---

## 概述 | Overview

自包含部署（Self-Contained Deployment）将 .NET Runtime 和所有依赖项打包到单个可执行文件中，使应用程序可以在没有安装 .NET SDK 或 Runtime 的机器上运行。

Self-contained deployment packages the .NET Runtime and all dependencies into a single executable, allowing the application to run on machines without .NET SDK or Runtime installed.

### 特性 | Features

- ✅ **无需安装 .NET Runtime** - 应用程序自带运行环境
  - No .NET Runtime installation required - Application includes its own runtime
- ✅ **单文件部署** - 所有文件打包到一个可执行文件
  - Single-file deployment - All files packaged into one executable
- ✅ **跨平台支持** - 支持 Windows 和 Linux
  - Cross-platform support - Supports Windows and Linux
- ✅ **ReadyToRun 优化** - 提升启动性能
  - ReadyToRun optimization - Improved startup performance
- ✅ **包含原生库** - 自动解压原生依赖
  - Includes native libraries - Automatically extracts native dependencies

---

## 快速开始 | Quick Start

### Windows x64

```powershell
# 执行发布脚本
.\publish-win-x64.ps1

# 运行发布的应用程序
.\publish\win-x64\ZakYip.WheelDiverterSorter.Host.exe
```

### Linux x64

```bash
# 执行发布脚本
./publish-linux-x64.sh

# 运行发布的应用程序
./publish/linux-x64/ZakYip.WheelDiverterSorter.Host
```

---

## 手动发布 | Manual Publishing

如果需要自定义发布选项，可以使用以下命令：

If you need to customize publish options, use the following commands:

### Windows x64

```bash
dotnet publish src/Host/ZakYip.WheelDiverterSorter.Host/ZakYip.WheelDiverterSorter.Host.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output ./publish/win-x64 \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishReadyToRun=true
```

### Linux x64

```bash
dotnet publish src/Host/ZakYip.WheelDiverterSorter.Host/ZakYip.WheelDiverterSorter.Host.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output ./publish/linux-x64 \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishReadyToRun=true
```

### 其他平台 | Other Platforms

支持的 Runtime Identifier (RID)：

Supported Runtime Identifiers (RID):

- `win-x64` - Windows 64-bit
- `win-x86` - Windows 32-bit
- `win-arm64` - Windows ARM64
- `linux-x64` - Linux 64-bit
- `linux-arm` - Linux ARM
- `linux-arm64` - Linux ARM64
- `osx-x64` - macOS Intel
- `osx-arm64` - macOS Apple Silicon

完整列表请参考：https://learn.microsoft.com/en-us/dotnet/core/rid-catalog

Full list: https://learn.microsoft.com/en-us/dotnet/core/rid-catalog

---

## 发布选项说明 | Publish Options Explanation

### `--self-contained true`
将 .NET Runtime 打包到应用程序中，无需目标机器预安装 Runtime。

Packages the .NET Runtime with the application, no Runtime installation required on target machine.

### `-p:PublishSingleFile=true`
将所有文件打包到单个可执行文件中，简化部署。

Packages all files into a single executable, simplifying deployment.

### `-p:IncludeNativeLibrariesForSelfExtract=true`
自动解压原生库（如雷赛 LTDMC.dll）到临时目录，确保正常运行。

Automatically extracts native libraries (e.g., Leadshine LTDMC.dll) to temp directory for proper execution.

### `-p:PublishReadyToRun=true`
启用 ReadyToRun (R2R) 预编译，减少启动时间和初次执行的 JIT 编译开销。

Enables ReadyToRun (R2R) pre-compilation, reducing startup time and initial JIT compilation overhead.

---

## 文件大小 | File Size

自包含部署会增加输出文件大小，因为包含了完整的 .NET Runtime：

Self-contained deployment increases output size because it includes the complete .NET Runtime:

- **Windows x64**: ~120-140 MB
- **Linux x64**: ~120-140 MB

可以通过以下方式减小文件大小：

You can reduce file size by:

1. **启用裁剪（Trimming）**（可能导致反射相关问题）：
   Enable trimming (may cause reflection-related issues):
   ```bash
   -p:PublishTrimmed=true
   ```

2. **仅框架依赖部署**（需要目标机器安装 .NET Runtime）：
   Framework-dependent deployment (requires .NET Runtime on target):
   ```bash
   --self-contained false
   ```

---

## 部署检查清单 | Deployment Checklist

发布前请确认：

Before publishing, confirm:

- [ ] 目标平台的 Runtime Identifier (RID) 正确
  - Target platform's Runtime Identifier (RID) is correct
- [ ] 配置文件（appsettings.json）已更新
  - Configuration files (appsettings.json) are updated
- [ ] 数据库连接字符串已配置
  - Database connection strings are configured
- [ ] 日志路径已配置（nlog.config）
  - Log paths are configured (nlog.config)
- [ ] 防火墙规则已设置（端口 5000）
  - Firewall rules are set (port 5000)

---

## 故障排查 | Troubleshooting

### 问题：无法找到 LTDMC.dll

**原因**：雷赛原生 DLL 未正确解压。

**解决方案**：
1. 确保使用了 `-p:IncludeNativeLibrariesForSelfExtract=true`
2. 检查是否有足够的临时目录权限

**Cause**: Leadshine native DLL not properly extracted.

**Solution**:
1. Ensure `-p:IncludeNativeLibrariesForSelfExtract=true` is used
2. Check temp directory permissions

---

### 问题：Linux 上提示 "Permission denied"

**解决方案**：添加可执行权限
```bash
chmod +x ZakYip.WheelDiverterSorter.Host
```

**Solution**: Add executable permission
```bash
chmod +x ZakYip.WheelDiverterSorter.Host
```

---

### 问题：应用启动失败

**诊断步骤**：

1. 检查系统依赖：
   ```bash
   ldd ZakYip.WheelDiverterSorter.Host
   ```

2. 查看详细日志：
   ```bash
   export COREHOST_TRACE=1
   ./ZakYip.WheelDiverterSorter.Host
   ```

**Diagnosis Steps**:

1. Check system dependencies:
   ```bash
   ldd ZakYip.WheelDiverterSorter.Host
   ```

2. View detailed logs:
   ```bash
   export COREHOST_TRACE=1
   ./ZakYip.WheelDiverterSorter.Host
   ```

---

## 性能注意事项 | Performance Considerations

### ReadyToRun 优化

自包含部署默认启用 ReadyToRun (R2R) 编译，可显著减少应用启动时间。

Self-contained deployment enables ReadyToRun (R2R) compilation by default, significantly reducing application startup time.

**优势 | Benefits**:
- ⚡ 更快的应用启动
  - Faster application startup
- 📉 减少初始 JIT 编译开销
  - Reduced initial JIT compilation overhead
- 🎯 可预测的性能特征
  - Predictable performance characteristics

**权衡 | Trade-offs**:
- 📦 稍大的可执行文件（+10-20%）
  - Slightly larger executable (+10-20%)
- ⏱️ 稍长的发布时间
  - Slightly longer publish time

---

## 技术说明 | Technical Notes

### 配置位置 | Configuration Location

自包含部署的配置在 `Host.csproj` 文件中：

Self-contained deployment configuration is in `Host.csproj`:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

### 临时文件位置 | Temp Files Location

单文件应用在首次运行时会解压到临时目录：

Single-file apps extract to temp directory on first run:

- **Windows**: `%TEMP%\.net\ZakYip.WheelDiverterSorter.Host\`
- **Linux**: `/tmp/.net/ZakYip.WheelDiverterSorter.Host/`

---

## 相关资源 | Related Resources

- [.NET 应用发布概述 | .NET App Publishing Overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [单文件部署 | Single-File Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [ReadyToRun 编译 | ReadyToRun Compilation](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)
- [Runtime Identifier 目录 | RID Catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)

---

## 反馈与支持 | Feedback & Support

如有问题或建议，请提交 Issue 或 Pull Request。

For issues or suggestions, please submit an Issue or Pull Request.
