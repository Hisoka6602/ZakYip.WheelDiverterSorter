# ZakYip.WheelDiverterSorter.Communication

通信层模块，负责与上游 RuleEngine 通信，请求包裹格口分配。

## 职责

- 实现 `IUpstreamRoutingClient` 接口
- 支持多种通信协议（TCP/SignalR/MQTT/HTTP）

## 支持的协议

| 协议 | 推荐环境 | 说明 |
|------|----------|------|
| TCP | 生产 | 低延迟，高吞吐 |
| SignalR | 生产 | 实时双向通信 |
| MQTT | 生产 | IoT 场景 |
| HTTP | 仅测试 | 性能较差 |

## 关键规范

> **禁止创建影分身**：所有协议实现必须直接实现 Core 层定义的 `IUpstreamRoutingClient` 接口。

- 接口定义位置：`Core/Abstractions/Upstream/`
- 扩展新协议只需实现接口并在工厂中注册
