# ZakYip.WheelDiverterSorter.Drivers

硬件驱动层，提供摆轮设备和 IO 控制的具体实现。

## 职责

- 实现 Core 层定义的 HAL 接口（`IWheelDiverterDriver`、`IInputPort`、`IOutputPort` 等）
- 支持多厂商设备（雷赛/西门子/摩迪/书迪鸟/仿真）

## 目录结构

```
Vendors/
├── Leadshine/    # 雷赛 IO 卡驱动
├── Siemens/      # 西门子 S7 PLC 驱动
├── Modi/         # 摩迪摆轮协议驱动
├── ShuDiNiao/    # 书迪鸟摆轮协议驱动
└── Simulated/    # 仿真驱动（开发测试用）
```

## 关键规范

> **禁止创建影分身**：所有厂商实现必须直接实现 Core 层定义的接口，不得在本项目中创建新的抽象接口。

- HAL 接口定义位置：`Core/Hardware/`
- 所有摆轮实现命名：`<VendorName>WheelDiverterDriver`
- 禁止使用 `*DiverterController` 命名
