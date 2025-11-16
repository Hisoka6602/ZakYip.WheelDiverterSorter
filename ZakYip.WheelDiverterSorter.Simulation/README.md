# 摆轮分拣系统仿真项目

## 简介

本项目是一个独立的仿真 Console 宿主，用于模拟摆轮分拣系统的运行。它通过依赖注入挂载"模拟驱动 + 模拟传感器 + 模拟上游 RuleEngine"，在本地一次性跑完一批"虚拟包裹"，并输出每个包裹的分拣结果与统计汇总。

## 特点

- **无需硬件**: 完全在内存中运行，不连接任何真实硬件设备
- **快速验证**: 快速验证分拣逻辑和路径生成算法
- **统计报告**: 自动生成中文统计报告，包括成功率、格口分布等
- **灵活配置**: 支持多种分拣模式和自定义参数

## 使用方法

### 运行仿真

```bash
dotnet run --project ZakYip.WheelDiverterSorter.Simulation
```

### 配置说明

编辑 `appsettings.Simulation.json` 来配置仿真参数：

```json
{
  "Simulation": {
    "ParcelCount": 50,                    // 包裹数量
    "LineSpeedMmps": 500,                 // 线速（毫米/秒）
    "ParcelInterval": "00:00:00.500",     // 包裹间隔（时间跨度格式）
    "SortingMode": "RoundRobin",          // 分拣模式：Formal / FixedChute / RoundRobin
    "FixedChuteIds": [1, 2, 3, 4, 5],    // 固定格口ID列表
    "ExceptionChuteId": 999,              // 异常格口ID
    "IsEnableRandomFaultInjection": false,// 是否启用随机故障注入
    "FaultInjectionProbability": 0.05,    // 故障注入概率
    "IsEnableVerboseLogging": true,       // 是否启用详细日志
    "IsPauseAtEnd": true                  // 是否在结束时等待用户按键
  }
}
```

### 分拣模式

- **RoundRobin**: 按顺序轮流分配到 `FixedChuteIds` 中的格口
- **FixedChute**: 随机分配到 `FixedChuteIds` 中的格口
- **Formal**: 使用包裹ID的哈希值模拟真实规则引擎的决策

### 命令行参数

也可以通过命令行参数覆盖配置：

```bash
dotnet run --project ZakYip.WheelDiverterSorter.Simulation -- \
  --Simulation:ParcelCount=100 \
  --Simulation:SortingMode=RoundRobin
```

## 输出示例

```
═══════════════════════════════════════════════════════════
                   仿真配置摘要
═══════════════════════════════════════════════════════════
包裹数量：          50
线速：              500 mm/s
包裹间隔：          0.50 秒
分拣模式：          RoundRobin
异常格口：          999
随机故障注入：      禁用
详细日志：          启用
═══════════════════════════════════════════════════════════

... (处理日志) ...

═══════════════════════════════════════════════════════════
                   仿真统计报告
═══════════════════════════════════════════════════════════
总包裹数：          50
成功分拣数：        50
失败分拣数：        0
成功率：            100.00 %
总耗时：            26.45 秒
平均每包耗时：      528.96 毫秒

格口分拣统计：
───────────────────────────────────────────────────────────
格口ID            分拣数量            百分比            
───────────────────────────────────────────────────────────
1               10              20.00%
2               10              20.00%
3               10              20.00%
4               10              20.00%
5               10              20.00%
═══════════════════════════════════════════════════════════
```

## 项目结构

```
ZakYip.WheelDiverterSorter.Simulation/
├── Configuration/
│   └── SimulationOptions.cs        # 仿真配置模型
├── Services/
│   ├── SimulationRunner.cs         # 仿真运行器
│   └── SimulationReportPrinter.cs  # 报告打印器
├── Scenarios/                       # 未来场景目录（待填充）
├── Program.cs                       # 入口程序
├── appsettings.Simulation.json     # 仿真配置文件
└── appsettings.Test.json           # 测试配置文件
```

## 依赖关系

本项目引用以下项目，使用其提供的接口和服务：

- `ZakYip.WheelDiverterSorter.Core` - 核心模型和接口
- `ZakYip.WheelDiverterSorter.Execution` - 路径执行器（使用模拟实现）
- `ZakYip.WheelDiverterSorter.Drivers` - 驱动程序（使用模拟实现）
- `ZakYip.WheelDiverterSorter.Ingress` - 入口检测
- `ZakYip.WheelDiverterSorter.Communication` - 通信层（使用内存实现）
- `ZakYip.WheelDiverterSorter.Observability` - 可观测性

## 扩展

未来可以在 `Scenarios/` 目录中添加各种仿真场景：

- 高负载场景
- 故障注入场景
- 特殊分拣规则场景
- 性能基准测试场景

## 注意事项

- 仿真使用模拟驱动器（`MockSwitchingPathExecutor`），不进行真实的硬件通信
- 仿真使用内存 RuleEngine 客户端（`InMemoryRuleEngineClient`），不连接真实的规则引擎
- 所有接口均来自现有层，没有仿真专用的硬编码路径
