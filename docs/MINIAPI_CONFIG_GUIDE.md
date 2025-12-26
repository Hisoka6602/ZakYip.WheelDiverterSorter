# MiniApi 配置说明

## 概述

MiniApi 配置允许您在 `appsettings.json` 中配置 API 服务的监听地址和 Swagger 文档的启用状态。

## 配置项说明

```json
"MiniApi": {
  "Urls": [
    "http://0.0.0.0:5000"
  ],
  "EnableSwagger": true
}
```

### Urls（监听地址列表）

配置 API 服务监听的地址和端口，支持多个地址同时监听。

**支持的格式**：

| 格式 | 说明 | 使用场景 |
|-----|------|---------|
| `http://localhost:5000` | 仅本地访问 | 开发环境、本地测试 |
| `http://0.0.0.0:5000` | 允许外部访问（所有IPv4接口） | 生产环境、容器部署 |
| `http://*:5000` | 绑定所有网络接口 | 跨平台兼容 |
| `https://0.0.0.0:5001` | HTTPS（需要配置证书） | 生产环境（加密通信） |

**多地址示例**：

```json
"Urls": [
  "http://0.0.0.0:5000",
  "https://0.0.0.0:5001"
]
```

### EnableSwagger（启用Swagger文档）

控制是否启用 Swagger API 文档界面。

**推荐配置**：

- **开发环境**: `true` - 方便 API 调试和测试
- **生产环境**: `false` - 提高安全性和性能，避免暴露 API 结构

## 配置示例

### 开发环境配置

```json
{
  "MiniApi": {
    "Urls": [ "http://localhost:5000" ],
    "EnableSwagger": true
  }
}
```

### 生产环境配置

```json
{
  "MiniApi": {
    "Urls": [ 
      "http://0.0.0.0:5000",
      "https://0.0.0.0:5001"
    ],
    "EnableSwagger": false
  }
}
```

### Docker/Kubernetes 部署配置

```json
{
  "MiniApi": {
    "Urls": [ "http://0.0.0.0:8080" ],
    "EnableSwagger": false
  }
}
```

## 访问 API

配置生效后，可以通过以下地址访问：

- **API 端点**: `http://<host>:<port>/api/...`
- **Swagger 文档**: `http://<host>:<port>/swagger` （仅当 EnableSwagger=true 时可用）

## 常见问题

### Q: 如何更改默认端口？

A: 修改 `Urls` 数组中的端口号即可，例如：

```json
"Urls": [ "http://0.0.0.0:8080" ]
```

### Q: 如何禁用 Swagger？

A: 将 `EnableSwagger` 设置为 `false`：

```json
"EnableSwagger": false
```

### Q: 如何配置 HTTPS？

A: 需要两步：

1. 在 `Urls` 中添加 HTTPS 地址：
   ```json
   "Urls": [ "https://0.0.0.0:5001" ]
   ```

2. 在 `appsettings.json` 中配置 Kestrel 证书（参考 ASP.NET Core 官方文档）

### Q: 如何在防火墙后使用？

A: 使用 `0.0.0.0` 或 `*` 绑定所有接口，并确保防火墙允许相应端口的入站连接。

## 注意事项

1. **安全性**: 生产环境建议禁用 Swagger 并配置 HTTPS
2. **端口冲突**: 确保配置的端口未被其他服务占用
3. **防火墙**: 外部访问需要开放相应的防火墙端口
4. **日志**: 配置变更会在启动日志中记录监听地址

## 默认值

如果未配置 `MiniApi` 节点，将使用以下默认值：

```json
{
  "MiniApi": {
    "Urls": [ "http://localhost:5000" ],
    "EnableSwagger": true
  }
}
```
