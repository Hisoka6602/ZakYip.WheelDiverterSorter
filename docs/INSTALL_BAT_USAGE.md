# install.bat 使用说明 (Install.bat Usage Guide)

## ⚠️ 重要提示 (Important Notice)

**`install.bat` 会从当前目录安装 Windows Service**，因此必须确保使用正确的构建版本。

**`install.bat` installs the Windows Service from the current directory**, so you must ensure you're using the correct build version.

---

## 问题：使用 install.bat 导致服务启动失败（错误 1053）

### 原因分析

当您直接在源代码目录或 Debug 构建目录运行 `install.bat` 时，会安装 **Debug 构建版本**，这会导致服务启动失败（错误 1053）。

**Debug 构建不适合用于 Windows Service 部署**，原因包括：
1. 缺少必要的性能优化
2. 包含调试符号和额外的开销
3. 启动时间较长，可能超过 Windows SCM 的超时限制

### 解决方案

有两种正确的安装方式：

---

## 方式一：使用发布脚本（推荐）✅

这是**最推荐**的方式，会生成优化的 Release 构建并自动配置。

### 步骤：

1. **在仓库根目录运行发布脚本**：
   ```powershell
   # 以管理员身份运行 PowerShell
   cd <仓库根目录>
   .\publish-win-x64.ps1
   ```

2. **进入发布目录**：
   ```powershell
   cd publish\win-x64
   ```

3. **运行该目录中的 install.bat**：
   ```bat
   install.bat
   ```

### 为什么这种方式最好？

- ✅ 自动使用 Release 配置
- ✅ 包含所有必要的依赖
- ✅ 启用了性能优化（ReadyToRun）
- ✅ 自包含部署，无需安装 .NET Runtime

---

## 方式二：手动构建 Release 版本

如果您想手动控制构建过程：

### 步骤：

1. **构建 Release 版本**：
   ```powershell
   # 在仓库根目录
   dotnet build src\Host\ZakYip.WheelDiverterSorter.Host\ZakYip.WheelDiverterSorter.Host.csproj --configuration Release
   ```

2. **进入 Release 构建目录**：
   ```powershell
   cd src\Host\ZakYip.WheelDiverterSorter.Host\bin\Release\net8.0
   ```

3. **运行该目录中的 install.bat**：
   ```bat
   install.bat
   ```

**注意**：此方式不是自包含部署，需要目标机器已安装 .NET 8.0 Runtime。

---

## ❌ 错误的使用方式

### 不要这样做：

```powershell
# ❌ 错误：直接在源代码目录运行
cd src\Host\ZakYip.WheelDiverterSorter.Host
install.bat

# ❌ 错误：在 Debug 构建目录运行
cd src\Host\ZakYip.WheelDiverterSorter.Host\bin\Debug\net8.0
install.bat
```

**后果**：
- 服务创建成功，但启动时报错 1053
- 日志中可能显示初始化失败或超时
- 性能和稳定性问题

---

## 验证安装是否正确

### 1. 检查服务状态

```powershell
Get-Service -Name ZakYip.WheelDiverterSorter
```

**正确的输出**：
```
Status   Name                               DisplayName
------   ----                               -----------
Running  ZakYip.WheelDiverterSorter        ZakYip.WheelDiverterSorter
```

### 2. 检查日志文件

查看安装目录下的 `Logs/` 文件夹，确认服务正常启动：

```
Logs/wheeldiverter-YYYY-MM-DD.log
```

应该能看到类似以下的日志：
```
========== 包裹检测服务启动完成 ==========
========== 分拣服务初始化完成 ==========
```

### 3. 测试 API 访问

```powershell
Invoke-WebRequest -Uri "http://localhost:5000/api/health" -Method Get
```

如果返回 HTTP 200，说明服务运行正常。

---

## 快速参考表

| 场景 | 应该使用的目录 | 命令 |
|------|---------------|------|
| **生产部署（推荐）** | `publish\win-x64\` | 先运行 `.\publish-win-x64.ps1`，然后 `cd publish\win-x64` 再 `install.bat` |
| **手动 Release 构建** | `bin\Release\net8.0\` | `dotnet build --configuration Release`，然后进入 Release 目录运行 `install.bat` |
| **❌ 错误：源代码目录** | `src\Host\...\` | 不要这样做！ |
| **❌ 错误：Debug 构建** | `bin\Debug\net8.0\` | 不要这样做！ |

---

## 故障排查

### 问题：安装后服务无法启动（错误 1053）

**检查清单**：

1. ✅ 是否使用了 Release 构建？
   ```powershell
   # 检查当前目录中是否包含优化的二进制文件
   # Release 构建通常文件更小，不包含 .pdb 调试符号文件
   ```

2. ✅ 是否在正确的目录运行 install.bat？
   - 应该在 `publish\win-x64\` 或 `bin\Release\net8.0\`
   - 不应该在源代码目录

3. ✅ 查看事件查看器
   ```
   Win + R -> eventvwr.msc
   -> Windows 日志 -> 应用程序
   -> 查找来源为 "ZakYip.WheelDiverterSorter" 的错误
   ```

4. ✅ 查看应用程序日志
   - 检查安装目录下的 `Logs/` 文件夹
   - 查看最新的日志文件

### 解决方案：

如果发现安装了错误的版本：

1. **卸载现有服务**：
   ```bat
   uninstall.bat
   ```

2. **使用正确的方式重新安装**（参见上面的"方式一"或"方式二"）

---

## 相关文档

- [Windows Service 部署指南](./WINDOWS_SERVICE_DEPLOYMENT.md) - 完整的部署文档
- [生产环境服务启动说明](./PRODUCTION_SERVICE_STARTUP.md) - 服务启动流程

---

**文档版本**: 1.0  
**创建日期**: 2025-12-18  
**维护团队**: ZakYip Development Team
