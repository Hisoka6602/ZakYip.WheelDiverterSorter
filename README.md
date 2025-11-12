# ZakYip.WheelDiverterSorter

直线摆轮分拣系统

## 项目结构

- **ZakYip.WheelDiverterSorter.Core**: 核心业务逻辑，包含路径生成器接口和实现
- **ZakYip.WheelDiverterSorter.Execution**: 执行层，包含路径执行器接口和模拟实现
- **ZakYip.WheelDiverterSorter.Host**: Web API 主机，提供调试接口
- **ZakYip.WheelDiverterSorter.Ingress**: 入口管理
- **ZakYip.WheelDiverterSorter.Observability**: 可观测性支持

## 调试接口

### 概述

Host 层提供了一个用于调试直线摆轮方案的最小接口。

**注意**：这是调试入口，正式环境可改成由扫码触发或供包台触发。

### API 端点

**POST** `/api/debug/sort`

#### 请求参数

```json
{
  "parcelId": "包裹ID",
  "targetChuteId": "目标格口ID"
}
```

#### 响应示例

成功案例：
```json
{
  "parcelId": "PKG001",
  "targetChuteId": "CHUTE_A",
  "isSuccess": true,
  "actualChuteId": "CHUTE_A",
  "message": "分拣成功：包裹 PKG001 已成功分拣到格口 CHUTE_A",
  "failureReason": null,
  "pathSegmentCount": 2
}
```

失败案例（未知格口）：
```json
{
  "parcelId": "PKG004",
  "targetChuteId": "CHUTE_UNKNOWN",
  "isSuccess": false,
  "actualChuteId": "未知",
  "message": "路径生成失败：目标格口无法映射到任何摆轮组合",
  "failureReason": "目标格口未配置或不存在",
  "pathSegmentCount": 0
}
```

### 使用示例

```bash
curl -X POST http://localhost:5000/api/debug/sort \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "PKG001", "targetChuteId": "CHUTE_A"}'
```

### 工作流程

1. 接收包裹ID和目标格口ID
2. 调用路径生成器（`ISwitchingPathGenerator`）生成 `SwitchingPath`
3. 调用执行器（`ISwitchingPathExecutor`）执行路径
4. 返回执行结果和实际落格ID

### 预配置的格口

当前默认配置包含以下格口映射：

- **CHUTE_A**: 需要经过摆轮D1（30度）和摆轮D2（45度）
- **CHUTE_B**: 需要经过摆轮D1（0度直行）
- **CHUTE_C**: 需要经过摆轮D1（90度）和摆轮D3（30度）

## 运行项目

```bash
cd ZakYip.WheelDiverterSorter.Host
dotnet run
```

默认监听端口：5000（HTTP）

## 开发

### 构建

```bash
dotnet build
```

### 测试

```bash
dotnet test
```
