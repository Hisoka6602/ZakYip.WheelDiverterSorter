# Windows Service 部署指南

本文档介绍如何将摆轮分拣系统部署为 Windows Service（Windows 服务）。

## 目录

1. [前提条件](#前提条件)
2. [发布应用程序](#发布应用程序)
3. [安装 Windows Service](#安装-windows-service)
4. [服务管理](#服务管理)
5. [配置说明](#配置说明)
6. [故障排查](#故障排查)
7. [卸载服务](#卸载服务)

---

## 前提条件

### 系统要求

- Windows Server 2016 或更高版本（推荐 Windows Server 2019/2022）
- Windows 10/11（开发/测试环境）
- .NET 8.0 Runtime（自包含部署时不需要）
- 管理员权限（安装/卸载服务需要）

### 硬件要求

- CPU：2核心或以上
- 内存：4GB 或以上
- 磁盘空间：至少 500MB 可用空间（用于应用程序和日志）

---

## 发布应用程序

### 方式一：使用发布脚本（推荐）

在仓库根目录运行以下 PowerShell 脚本：

```powershell
.\publish-win-x64.ps1
```

脚本会自动：
- 清理旧的发布输出
- 编译 Release 版本
- 生成自包含部署包（包含 .NET Runtime）
- 启用 ReadyToRun 编译优化
- 输出到 `publish/win-x64/` 目录

### 方式二：手动发布

```powershell
dotnet publish src/Host/ZakYip.WheelDiverterSorter.Host/ZakYip.WheelDiverterSorter.Host.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output ./publish/win-x64 `
    -p:PublishReadyToRun=true
```

### 发布输出说明

发布成功后，`publish/win-x64/` 目录包含：
- `ZakYip.WheelDiverterSorter.Host.exe` - 主可执行文件
- `appsettings.json` - 应用配置文件
- `nlog.config` - 日志配置文件
- `Data/` - 数据库目录（运行后自动创建）
- `Logs/` - 日志目录（运行后自动创建）
- 其他 DLL 和依赖文件

---

## 安装 Windows Service

### 步骤 1：准备部署目录

1. 将 `publish/win-x64/` 目录复制到目标服务器
2. 推荐部署路径：`C:\Program Files\WheelDiverterSorter\`
3. 确保目录有足够的读写权限

示例：
```powershell
# 创建部署目录
New-Item -ItemType Directory -Path "C:\Program Files\WheelDiverterSorter" -Force

# 复制发布文件
Copy-Item -Path ".\publish\win-x64\*" -Destination "C:\Program Files\WheelDiverterSorter\" -Recurse -Force
```

### 步骤 2：运行安装脚本

以管理员身份运行 PowerShell，然后执行：

```powershell
# 使用默认配置安装
.\install-windows-service.ps1

# 或者自定义安装参数
.\install-windows-service.ps1 `
    -ServiceName "WheelDiverterSorter" `
    -DisplayName "摆轮分拣系统服务" `
    -BinaryPath "C:\Program Files\WheelDiverterSorter\ZakYip.WheelDiverterSorter.Host.exe" `
    -StartupType "Automatic"
```

### 安装参数说明

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `-ServiceName` | Windows 服务名称（用于命令行管理） | `WheelDiverterSorter` |
| `-DisplayName` | 服务显示名称（在服务管理器中显示） | `摆轮分拣系统服务 (Wheel Diverter Sorter Service)` |
| `-Description` | 服务描述 | 自动化包裹分拣服务 |
| `-BinaryPath` | 可执行文件完整路径 | `.\publish\win-x64\ZakYip.WheelDiverterSorter.Host.exe` |
| `-StartupType` | 启动类型（Automatic/Manual/Disabled） | `Automatic` |

### 步骤 3：验证安装

安装完成后，可以通过以下方式验证：

#### 方式一：PowerShell 命令
```powershell
Get-Service -Name WheelDiverterSorter
```

#### 方式二：服务管理器
1. 按 `Win + R`，输入 `services.msc`
2. 查找"摆轮分拣系统服务"
3. 验证服务状态和启动类型

---

## 服务管理

### 启动服务

```powershell
# PowerShell
Start-Service -Name WheelDiverterSorter

# 或使用 sc 命令
sc.exe start WheelDiverterSorter

# 或使用 net 命令
net start WheelDiverterSorter
```

### 停止服务

```powershell
# PowerShell
Stop-Service -Name WheelDiverterSorter

# 或使用 sc 命令
sc.exe stop WheelDiverterSorter

# 或使用 net 命令
net stop WheelDiverterSorter
```

### 重启服务

```powershell
# PowerShell
Restart-Service -Name WheelDiverterSorter

# 或分步操作
Stop-Service -Name WheelDiverterSorter
Start-Service -Name WheelDiverterSorter
```

### 查看服务状态

```powershell
# 详细状态信息
Get-Service -Name WheelDiverterSorter | Format-List

# 仅查看状态
Get-Service -Name WheelDiverterSorter | Select-Object Name, Status, StartType
```

### 设置启动类型

```powershell
# 设置为自动启动
Set-Service -Name WheelDiverterSorter -StartupType Automatic

# 设置为手动启动
Set-Service -Name WheelDiverterSorter -StartupType Manual

# 禁用服务
Set-Service -Name WheelDiverterSorter -StartupType Disabled
```

---

## 配置说明

### 工作目录

Windows Service 默认工作目录为可执行文件所在目录。所有相对路径都基于此目录：

- **数据库文件**: `Data/routes.db`
- **日志目录**: `Logs/`
- **配置文件**: `appsettings.json`、`nlog.config`

### 日志配置

服务运行时的日志配置位于 `nlog.config`：

```xml
<!-- 日志文件路径 -->
<target name="logfile" xsi:type="File"
        fileName="${basedir}/Logs/wheeldiverter-${shortdate}.log" />
```

**重要提示**：
- 日志默认保存在 `Logs/` 目录
- 日志文件按日期命名（如 `wheeldiverter-2024-12-14.log`）
- 确保服务账户对日志目录有写权限

### 数据库配置

系统使用 LiteDB 数据库存储配置信息：

- **数据库路径**: `Data/routes.db`（相对于工作目录）
- **首次运行**: 自动创建数据库和默认配置
- **备份建议**: 定期备份 `Data/` 目录

### 服务账户权限

默认情况下，Windows Service 使用 `Local System` 账户运行。如需使用其他账户：

```powershell
# 设置服务账户（需要管理员权限）
sc.exe config WheelDiverterSorter obj= ".\ServiceAccount" password= "YourPassword"

# 或使用服务管理器图形界面
# 1. 打开 services.msc
# 2. 右键服务 -> 属性 -> 登录
# 3. 选择"此账户"并输入账户信息
```

**权限要求**：
- 读取应用程序目录
- 写入日志目录 (`Logs/`)
- 写入数据库目录 (`Data/`)
- 网络访问权限（如果需要连接上游系统）
- 硬件设备访问权限（如需访问 IO 板卡或摆轮设备）

---

## 故障排查

### 服务无法启动

#### 症状
- 服务状态显示"已停止"
- 服务管理器报错"无法启动服务"

#### 排查步骤

1. **查看事件查看器**
   ```
   Win + R -> eventvwr.msc
   -> Windows 日志 -> 应用程序
   -> 查找来源为 "WheelDiverterSorter" 的错误
   ```

2. **检查日志文件**
   - 打开 `Logs/` 目录
   - 查看最新的日志文件
   - 搜索 "Error"、"Exception"、"Failed" 等关键词

3. **验证权限**
   ```powershell
   # 检查服务账户对目录的权限
   icacls "C:\Program Files\WheelDiverterSorter"
   ```

4. **手动测试启动**
   ```powershell
   # 直接运行可执行文件测试
   cd "C:\Program Files\WheelDiverterSorter"
   .\ZakYip.WheelDiverterSorter.Host.exe
   
   # 观察控制台输出的错误信息
   ```

5. **验证配置文件**
   - 检查 `appsettings.json` 格式是否正确
   - 检查 `nlog.config` 是否存在
   - 确保 `Data/` 目录可写

### 服务运行但功能异常

#### 症状
- 服务状态显示"正在运行"
- 但分拣功能不工作或报错

#### 排查步骤

1. **检查应用日志**
   ```powershell
   # 查看最新日志
   Get-Content "C:\Program Files\WheelDiverterSorter\Logs\wheeldiverter-$(Get-Date -Format yyyy-MM-dd).log" -Tail 100
   ```

2. **检查 API 端点**
   ```powershell
   # 测试健康检查端点
   Invoke-WebRequest -Uri "http://localhost:5000/api/health" -Method Get
   
   # 查看 Swagger 文档
   Start-Process "http://localhost:5000/swagger"
   ```

3. **检查配置**
   - 访问 Swagger UI: `http://localhost:5000/swagger`
   - 使用配置管理 API 验证系统配置、驱动配置、传感器配置等

4. **检查硬件连接**
   - 如使用硬件 IO 板卡，确认设备驱动已安装
   - 验证硬件连接状态
   - 检查厂商驱动 DLL 是否存在

### 服务频繁重启

#### 症状
- 服务自动停止后重启
- 事件日志显示多次启动记录

#### 排查步骤

1. **检查异常日志**
   - 查看日志中的未捕获异常
   - 关注 "Stopped program because of exception" 日志

2. **检查资源占用**
   ```powershell
   # 查看服务进程资源占用
   Get-Process -Name "ZakYip.WheelDiverterSorter.Host" | Format-List
   ```

3. **验证依赖项**
   - 确认所有 DLL 文件完整
   - 验证厂商驱动 DLL 版本兼容性

### 常见错误码

| 错误代码 | 说明 | 解决方案 |
|---------|------|---------|
| 1053 | 服务在指定时间内未响应 | 增加服务启动超时时间，检查启动逻辑 |
| 1067 | 进程意外终止 | 查看应用日志，修复启动错误 |
| 5 | 拒绝访问 | 检查服务账户权限 |
| 2 | 找不到指定的文件 | 验证可执行文件路径和依赖文件 |

---

## 卸载服务

### 使用卸载脚本（推荐）

以管理员身份运行：

```powershell
.\uninstall-windows-service.ps1
```

脚本会自动：
1. 停止服务（如果正在运行）
2. 删除服务注册
3. 验证服务是否成功删除

### 手动卸载

```powershell
# 1. 停止服务
Stop-Service -Name WheelDiverterSorter -Force

# 2. 删除服务
sc.exe delete WheelDiverterSorter

# 3. 验证删除
Get-Service -Name WheelDiverterSorter
# 应该显示 "找不到服务"
```

### 清理文件（可选）

卸载服务后，可以选择清理应用程序文件和数据：

```powershell
# 删除应用程序目录
Remove-Item -Path "C:\Program Files\WheelDiverterSorter" -Recurse -Force

# 注意：这会删除所有日志和数据库文件
# 建议先备份 Data/ 和 Logs/ 目录
```

---

## 最佳实践

### 生产环境建议

1. **自动启动配置**
   - 将服务设置为"自动"启动类型
   - 配置服务失败恢复策略（自动重启）

2. **日志管理**
   - 定期归档或清理旧日志文件
   - 配置日志轮转策略（参考 `nlog.config`）
   - 监控日志目录磁盘空间

3. **备份策略**
   - 定期备份 `Data/routes.db` 数据库
   - 备份 `appsettings.json` 和 `nlog.config`
   - 保留发布包副本以便快速恢复

4. **监控告警**
   - 监控服务运行状态
   - 配置性能计数器告警
   - 通过日志文件监控系统健康状态

5. **安全加固**
   - 使用专用服务账户（避免使用 Local System）
   - 限制服务账户权限（最小权限原则）
   - 定期更新系统和依赖项

### 升级服务

1. 停止服务
   ```powershell
   Stop-Service -Name WheelDiverterSorter
   ```

2. 备份当前版本
   ```powershell
   Copy-Item -Path "C:\Program Files\WheelDiverterSorter" `
             -Destination "C:\Program Files\WheelDiverterSorter.backup" `
             -Recurse
   ```

3. 覆盖新版本文件
   ```powershell
   Copy-Item -Path ".\publish\win-x64\*" `
             -Destination "C:\Program Files\WheelDiverterSorter\" `
             -Recurse -Force
   ```

4. 保留数据和配置
   - 不要覆盖 `Data/` 目录
   - 检查 `appsettings.json` 和 `nlog.config` 是否有新增配置项

5. 启动服务
   ```powershell
   Start-Service -Name WheelDiverterSorter
   ```

6. 验证升级
   - 检查服务状态
   - 查看日志确认版本信息
   - 测试关键功能

---

## 参考链接

- [生产环境服务启动说明](./PRODUCTION_SERVICE_STARTUP.md)
- [上游连接配置指南](./guides/UPSTREAM_CONNECTION_GUIDE.md)
- [系统配置指南](./guides/SYSTEM_CONFIG_GUIDE.md)
- [API 使用指南](./guides/API_USAGE_GUIDE.md)

---

**文档版本**: 1.0  
**最后更新**: 2024-12-14  
**维护团队**: ZakYip Development Team
