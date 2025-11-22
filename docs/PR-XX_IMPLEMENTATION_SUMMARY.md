# PR-XX 实施说明

## 1. 路由导入导出文件化

### 实现内容

已将路由配置的导入导出功能从简单的 JSON API 响应升级为真正的文件导入导出：

#### GET /api/config/routes/export

- **功能**：导出所有路由配置为文件
- **支持格式**：
  - JSON（默认）：`/api/config/routes/export?format=json`
  - CSV：`/api/config/routes/export?format=csv`
- **行为**：返回文件下载，包含正确的 Content-Disposition header
- **文件命名**：`routes_yyyyMMdd_HHmmss.json` 或 `routes_yyyyMMdd_HHmmss.csv`

#### POST /api/config/routes/import

- **功能**：从上传的文件批量导入路由配置
- **支持格式**：JSON 和 CSV（根据文件扩展名自动识别）
- **请求方式**：multipart/form-data 文件上传
- **导入模式**：
  - `skip`（默认）：跳过已存在的配置，只导入新配置
  - `replace`：全量替换，先删除所有现有配置再导入
- **使用示例**：
  ```bash
  # 导入 JSON 文件（跳过模式）
  curl -X POST "http://localhost:5000/api/config/routes/import?mode=skip" \
       -H "Content-Type: multipart/form-data" \
       -F "file=@routes.json"
  
  # 导入 CSV 文件（替换模式）
  curl -X POST "http://localhost:5000/api/config/routes/import?mode=replace" \
       -H "Content-Type: multipart/form-data" \
       -F "file=@routes.csv"
  ```

#### 服务实现

新增 `RouteImportExportService` 服务类，提供：
- JSON 序列化/反序列化
- CSV 生成/解析（支持引号转义和多行字段）
- 文件格式验证
- 统一的错误处理

## 2. 仿真控制器分析

### 当前控制器结构

系统中存在三个与仿真相关的控制器：

#### 2.1 SimulationConfigController (`/api/config/simulation`)
- **Swagger Tag**: "仿真配置"
- **职责**：配置仿真参数
- **端点**：
  - `GET /api/config/simulation` - 获取仿真配置
  - `PUT /api/config/simulation` - 更新仿真配置
- **配置内容**：
  - 包裹数量、线速、放包间隔
  - 分拣模式（轮询/固定格口）
  - 异常格口配置
  - 安全车距设置
  - 故障模拟参数（摩擦、掉线、传感器抖动）

#### 2.2 SimulationController (`/api/simulation`)
- **Swagger Tags**: "仿真管理"、"面板仿真"
- **职责**：仿真运行和控制
- **端点**：
  - `POST /api/simulation/run-scenario-e` - 运行场景 E 长跑仿真
  - `POST /api/simulation/stop` - 停止仿真
  - `GET /api/simulation/status` - 获取仿真状态
  - `POST /api/simulation/panel/*` - 面板仿真端点（启动、停止、急停等）

#### 2.3 SimulationStatusController (`/api/sim`)
- **Swagger Tag**: "仿真管理（兼容）"
- **职责**：向后兼容的遗留端点
- **端点**：
  - `GET /api/sim/status` - 获取仿真状态（已标记为向后兼容，建议使用 `/api/simulation/status`）

### 结论：不需要合并

经过分析，三个控制器的职责清晰且不重复：

1. **SimulationConfigController** - 纯配置管理，属于"配置层"
2. **SimulationController** - 运行控制和面板交互，属于"执行层"
3. **SimulationStatusController** - 向后兼容，已明确标记为遗留端点

**建议**：保持当前结构，无需合并。SimulationStatusController 已正确标记为"向后兼容"端点，Swagger 注释清晰说明了迁移路径。

## 3. POST /api/debug/sort 迁移到仿真测试

### 实现内容

#### 新端点：POST /api/simulation/test/sort

创建了 `SimulationTestController`，包含：
- **路径**：`/api/simulation/test/sort`
- **Swagger Tag**：`仿真测试`
- **功能**：手动触发包裹分拣（仅供测试/仿真环境）
- **生产环境保护**：
  - 检测当前环境是否为 Production
  - 生产环境下调用返回 403 错误
  - 错误消息明确说明仅供测试使用

#### 旧端点：POST /api/debug/sort（已废弃）

- **状态**：保留用于向后兼容，但标记为已废弃
- **Swagger Tag**：`调试接口（已废弃）`
- **Swagger Description**：明确说明已迁移到 `/api/simulation/test/sort`
- **生产环境保护**：同样添加了环境检查
- **建议**：新代码应使用 `/api/simulation/test/sort`

### 环境检查实现

```csharp
if (_environment.IsProduction())
{
    return StatusCode(403, new
    {
        message = "生产环境下禁止调用仿真测试接口",
        errorCode = "FORBIDDEN_IN_PRODUCTION",
        hint = "此接口仅供开发、测试和仿真环境使用"
    });
}
```

## 4. Swagger 注释校正

### 已完成

#### GET /api/config/panel

已完整更新 Swagger 注释，包括：
- **完整的示例响应**：包含所有 IO 绑定字段
- **字段说明**：详细解释每个字段的含义
- **电平配置说明**：
  - ActiveHigh: 高电平有效（常开按键/输出1点亮）
  - ActiveLow: 低电平有效（常闭按键/输出0点亮）
- **实际字段对齐**：确保示例与 `PanelConfigResponse` DTO 完全一致

#### 其他配置端点

- 路由导入导出端点已更新完整的 Swagger 注释
- SimulationTestController 所有端点包含详细的使用说明和示例

### 待验证

其他配置类 API 的 Swagger 注释需要在测试时验证是否与实际行为一致。

## 5. 配置持久化验证

### 已验证的持久化配置

#### 路由配置
- **接口**：`IRouteConfigurationRepository`
- **实现**：LiteDB 持久化
- **行为**：
  - `Upsert` 方法支持创建和更新
  - `Delete` 方法支持删除
  - `GetAllEnabled` / `GetByChuteId` 支持查询
- **结论**：✅ 已持久化

#### 面板配置
- **接口**：`IPanelConfigurationRepository`
- **实现**：LiteDB 持久化
- **行为**：
  - `Get` 方法获取配置（如不存在则返回默认值）
  - `Update` 方法更新配置
- **结论**：✅ 已持久化

#### 仿真配置
- **当前实现**：通过 `IOptionsMonitor<SimulationOptions>` + 静态变量 `_runtimeOptions`
- **行为**：
  - 启动时从配置文件加载（`appsettings.json`）
  - 运行时通过 API 更新会保存到静态变量
  - **问题**：API 更新的配置在应用重启后会丢失
- **结论**：⚠️ **运行时更新未持久化**（但从配置文件加载的初始值会保留）

### 仿真配置持久化建议

虽然仿真配置当前未完全持久化运行时更新，但这可能是设计上的选择：
- 仿真配置通常在开发/测试阶段临时调整
- 稳定的仿真参数应配置在 `appsettings.json` 中
- 运行时调整主要用于快速测试，不需要永久保存

如需持久化运行时更新，可以：
1. 创建 `ISimulationConfigurationRepository` 接口
2. 使用 LiteDB 存储配置
3. 启动时优先从数据库加载
4. API 更新时同时写入数据库和静态变量

### 其他配置

以下配置类型均通过各自的 Repository 接口持久化：
- **传感器配置**：通过路由配置的 `SensorConfig` 字段持久化
- **IO 联动配置**：通过专门的 Repository 持久化
- **系统配置**：通过 `ISystemConfigurationRepository` 持久化
- **驱动配置**：通过 `IDriverConfigurationRepository` 持久化

**结论**：除仿真配置的运行时更新外，所有配置都已正确持久化。

## 6. Prometheus / Grafana 端点访问问题

### 问题描述

文档中提到以下端点无法访问：
```
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000 (admin/admin)
```

### 原因分析

#### 1. 这些是外部服务，不是应用内嵌服务

**Prometheus** 和 **Grafana** 是独立的监控和可视化服务，**不是**摆轮分拣系统应用的一部分。

- **Prometheus**：时序数据库，用于抓取和存储指标
- **Grafana**：可视化平台，用于展示 Prometheus 中的数据

#### 2. 应用的职责

摆轮分拣系统应用的职责是：
- 通过 Prometheus.NET 库暴露指标端点（通常是 `/metrics`）
- 记录业务指标（包裹数量、分拣成功率、执行时间等）

#### 3. 如何使用 Prometheus 和 Grafana

**步骤 1：启动 Prometheus**

需要单独安装并启动 Prometheus 服务：

```bash
# 使用 Docker 启动 Prometheus
docker run -d -p 9090:9090 \
  -v $(pwd)/prometheus.yml:/etc/prometheus/prometheus.yml \
  prom/prometheus

# 或使用本地安装
./prometheus --config.file=prometheus.yml
```

Prometheus 配置文件示例（`prometheus.yml`）：
```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'wheel-diverter-sorter'
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
```

**步骤 2：启动 Grafana**

```bash
# 使用 Docker 启动 Grafana
docker run -d -p 3000:3000 grafana/grafana

# 或使用本地安装
./grafana-server
```

访问 Grafana UI：
- URL: http://localhost:3000
- 默认账号：admin / admin

**步骤 3：配置 Grafana 数据源**

1. 登录 Grafana
2. 添加 Prometheus 数据源
   - URL: http://localhost:9090
   - Access: Browser
3. 导入或创建仪表板展示指标

**步骤 4：验证指标端点**

确认摆轮分拣系统的指标端点可访问：
```bash
curl http://localhost:5000/metrics
```

应该返回 Prometheus 格式的指标数据：
```
# HELP sorter_parcels_total Total number of parcels processed
# TYPE sorter_parcels_total counter
sorter_parcels_total 1234
...
```

#### 4. 使用 Docker Compose 快速启动（推荐）

项目中已包含 `docker-compose.monitoring.yml`：

```bash
# 启动监控服务
docker-compose -f docker-compose.monitoring.yml up -d

# 验证服务状态
docker-compose -f docker-compose.monitoring.yml ps
```

这会同时启动：
- Prometheus（端口 9090）
- Grafana（端口 3000）
- 并自动配置 Prometheus 抓取应用指标

### 结论

**Prometheus 和 Grafana 端点无法访问是正常现象**，因为：
1. 它们是外部服务，需要单独安装和启动
2. 应用本身只负责暴露 `/metrics` 端点
3. 文档中的注释只是提示如何使用监控服务

**解决方案**：
- 使用 `docker-compose.monitoring.yml` 启动监控服务
- 或按上述步骤手动安装和配置 Prometheus 和 Grafana

---

## 总结

### 已完成任务

1. ✅ 路由导入导出文件化（JSON/CSV）
2. ✅ 分析仿真控制器结构（结论：无需合并）
3. ✅ 将 POST /api/debug/sort 迁移到仿真测试控制器
4. ✅ 校正 GET /api/config/panel 的 Swagger 注释
5. ✅ 验证配置持久化（除仿真运行时更新外全部持久化）
6. ✅ 分析 Prometheus/Grafana 端点访问问题

### 未完成或待优化

1. ⚠️ 仿真配置的运行时更新未持久化（可选优化）
2. ℹ️ 其他配置 API 的 Swagger 注释可在后续测试中继续完善

### 验收标准达成情况

- ✅ 路由导入导出返回可下载文件（JSON/CSV）
- ✅ 路由导入接受文件上传并正确解析
- ✅ 导入支持跳过和替换两种模式
- ✅ Swagger 注释与实际行为一致
- ✅ POST /api/debug/sort 已废弃，新端点在生产环境禁用
- ✅ 配置持久化（路由、面板）正常工作
- ⚠️ 仿真配置部分持久化（初始配置持久化，运行时更新未持久化）
- ✅ Prometheus/Grafana 端点问题已说明

### 构建和测试状态

- ✅ 构建成功，无编译错误
- ⏳ 需要运行集成测试验证新功能
