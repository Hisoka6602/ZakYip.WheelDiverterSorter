# 性能分析工具 (Performance Profiling Tools)

本目录包含用于分析 ZakYip.WheelDiverterSorter 系统运行时性能的工具和脚本。

## 工具列表

### 1. trace-sampling (性能采样)

使用 `dotnet-trace` 对运行中的应用程序进行性能采样，生成可供分析的追踪文件。

#### Windows (PowerShell)

```powershell
# 基本用法：采样 30 秒
.\trace-sampling.ps1 -ProcessId 12345

# 采样 60 秒
.\trace-sampling.ps1 -ProcessId 12345 -Duration 60

# 详细 GC 采样
.\trace-sampling.ps1 -ProcessId 12345 -Profile gc-verbose

# 指定输出文件
.\trace-sampling.ps1 -ProcessId 12345 -OutputPath my-trace.nettrace
```

#### Linux/Mac (Bash)

```bash
# 基本用法
./trace-sampling.sh -p 12345

# 采样 60 秒并指定输出
./trace-sampling.sh -p 12345 -d 60 -o my-trace.nettrace

# 详细 GC 采样
./trace-sampling.sh -p 12345 -f gc-verbose
```

**采样配置 (Profile):**
- `cpu-sampling` (默认): CPU 性能采样，适用于查找性能瓶颈
- `gc-verbose`: 详细的 GC 信息，适用于内存分析
- `gc-collect`: GC 收集事件，适用于 GC 压力分析

### 2. counters-monitor (实时监控)

使用 `dotnet-counters` 实时监控应用程序的性能指标。

#### Windows (PowerShell)

```powershell
# 综合监控
.\counters-monitor.ps1 -ProcessId 12345

# 仅监控 GC
.\counters-monitor.ps1 -ProcessId 12345 -GcOnly

# 仅监控 CPU
.\counters-monitor.ps1 -ProcessId 12345 -CpuOnly

# 仅监控内存
.\counters-monitor.ps1 -ProcessId 12345 -MemoryOnly

# 保存到 CSV
.\counters-monitor.ps1 -ProcessId 12345 -OutputFile metrics.csv

# 每 5 秒刷新
.\counters-monitor.ps1 -ProcessId 12345 -RefreshInterval 5
```

#### Linux/Mac (Bash)

```bash
# 综合监控
./counters-monitor.sh -p 12345

# 仅监控 GC
./counters-monitor.sh -p 12345 -m gc

# 仅监控 CPU
./counters-monitor.sh -p 12345 -m cpu

# 仅监控内存
./counters-monitor.sh -p 12345 -m memory

# 保存到 CSV
./counters-monitor.sh -p 12345 -o metrics.csv

# 每 5 秒刷新
./counters-monitor.sh -p 12345 -r 5
```

## 使用场景

### 场景 1: 现场性能问题诊断

当生产环境遇到性能问题时：

1. 使用 `counters-monitor` 快速查看实时指标，判断问题类型
2. 如果怀疑 CPU 瓶颈，使用 `trace-sampling` 进行 CPU 采样
3. 如果怀疑 GC 压力，使用 `trace-sampling -Profile gc-verbose`

### 场景 2: 日常性能监控

定期监控系统健康状态：

```bash
# 监控 10 分钟并保存数据
./counters-monitor.sh -p $(pgrep -f "WheelDiverterSorter") -o daily-metrics.csv &
sleep 600
pkill -f counters-monitor
```

### 场景 3: 性能优化前后对比

在优化代码前后进行对比：

```bash
# 优化前
./trace-sampling.sh -p 12345 -o before-optimization.nettrace -d 60

# 优化后
./trace-sampling.sh -p 12345 -o after-optimization.nettrace -d 60

# 对比两个追踪文件
```

## 关键性能指标

### CPU 指标
- **cpu-usage**: CPU 使用率，正常应 < 70%
- **threadpool-thread-count**: 线程池线程数，应稳定
- **threadpool-queue-length**: 线程池队列长度，应接近 0

### 内存指标
- **working-set**: 工作集内存，不应持续增长
- **gc-heap-size**: GC 堆大小，应在合理范围内
- **alloc-rate**: 内存分配速率，高频场景应 < 100 MB/s

### GC 指标
- **gen-0-gc-count**: Gen0 GC 次数，频繁但快速
- **gen-1-gc-count**: Gen1 GC 次数，应较少
- **gen-2-gc-count**: Gen2 GC 次数，应很少
- **time-in-gc**: GC 时间占比，应 < 10%

### 异常指标
- **exception-count**: 异常数量，应接近 0（排除正常业务异常）

## 分析工具

生成的 `.nettrace` 文件可以使用以下工具分析：

1. **PerfView** (Windows)
   - 下载：https://github.com/microsoft/perfview/releases
   - 功能强大，适合深度分析

2. **Visual Studio** (Windows/Mac)
   - 菜单：文件 -> 打开 -> 文件 -> 选择 .nettrace
   - 集成的性能分析器

3. **speedscope** (跨平台)
   - 在线版：https://www.speedscope.app/
   - 拖放 .nettrace 文件即可分析
   - 火焰图可视化

## 前置要求

### 安装 .NET 诊断工具

工具会自动尝试安装，但也可以手动安装：

```bash
# 安装 dotnet-trace
dotnet tool install --global dotnet-trace

# 安装 dotnet-counters
dotnet tool install --global dotnet-counters

# 更新工具到最新版本
dotnet tool update --global dotnet-trace
dotnet tool update --global dotnet-counters
```

### 权限要求

- Linux/Mac: 可能需要 root 权限或 CAP_SYS_PTRACE 能力
- Windows: 需要有足够权限访问目标进程

### 脚本执行权限 (Linux/Mac)

```bash
chmod +x trace-sampling.sh
chmod +x counters-monitor.sh
```

## 最佳实践

1. **采样时长**: 通常 30-60 秒足够捕获性能问题
2. **生产环境**: 谨慎使用，采样会有轻微性能开销（通常 < 5%）
3. **磁盘空间**: 确保有足够空间存储追踪文件（通常 50-500 MB）
4. **文件管理**: 定期清理旧的追踪文件
5. **基线对比**: 建立性能基线，定期对比变化

## 故障排查

### 问题: 找不到进程 ID

```bash
# 查找进程
ps aux | grep WheelDiverterSorter
# 或者
dotnet-trace ps
```

### 问题: 工具未安装

确保 .NET SDK 已安装且在 PATH 中：

```bash
dotnet --version
dotnet tool list -g
```

### 问题: 权限不足 (Linux)

```bash
# 临时提升权限
sudo ./trace-sampling.sh -p 12345

# 或添加 CAP_SYS_PTRACE 能力
sudo setcap cap_sys_ptrace=eip /usr/share/dotnet/dotnet
```

## 参考资料

- [dotnet-trace 官方文档](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
- [dotnet-counters 官方文档](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters)
- [.NET 性能诊断](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/)
