# ZakYip.WheelDiverterSorter.Ingress

IO 传感器监听模块，负责感应包裹并触发检测事件。

## 职责

- 实现 `ISensor` 和 `IParcelDetectionService` 接口
- 支持多厂商传感器（雷赛/仿真）
- 包裹去重和防抖动

## 关键规范

> **禁止创建影分身**：所有传感器实现必须直接实现 Core 层定义的接口。

- 接口定义位置：`Core/Abstractions/Ingress/`
- 扩展新厂商只需实现接口并在工厂中注册
