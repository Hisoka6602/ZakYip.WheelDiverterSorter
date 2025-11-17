# Monitoring Infrastructure / 监控基础设施

This directory contains configuration files for the Prometheus and Grafana monitoring stack.
本目录包含Prometheus和Grafana监控栈的配置文件。

## Directory Structure / 目录结构

```
monitoring/
├── prometheus/
│   ├── prometheus.yml      # Prometheus main configuration / Prometheus主配置
│   └── alerts.yml          # Alert rules / 告警规则
├── grafana/
│   ├── provisioning/
│   │   ├── datasources/
│   │   │   └── prometheus.yml  # Datasource provisioning / 数据源配置
│   │   └── dashboards/
│   │       └── dashboards.yml  # Dashboard provisioning / 仪表板配置
│   └── dashboards/
│       └── wheel-diverter-sorter.json  # Main dashboard / 主仪表板
├── run-scenario-e-longrun.sh   # 场景 E 启动脚本 (Linux/macOS)
└── run-scenario-e-longrun.ps1  # 场景 E 启动脚本 (Windows)
```

## 🎯 场景 E 长跑仿真 (PR-05) / Scenario E Long-Run Simulation

场景 E 是专门为 Observability 验收设计的长时间仿真场景。

**Scenario E** is a long-run simulation specifically designed for Observability acceptance testing.

### 快速启动 / Quick Start

**Linux/macOS:**
```bash
./monitoring/run-scenario-e-longrun.sh
```

**Windows PowerShell:**
```powershell
.\monitoring\run-scenario-e-longrun.ps1
```

**自定义参数 / Custom Parameters:**
```bash
# 指定包裹数量 / Specify parcel count
PARCEL_COUNT=500 ./monitoring/run-scenario-e-longrun.sh

# 指定运行时长 / Specify duration
LONG_RUN_DURATION="00:05:00" ./monitoring/run-scenario-e-longrun.sh

# 不启动监控栈 / Skip monitoring stack
START_MONITORING=false ./monitoring/run-scenario-e-longrun.sh
```

### 场景特点 / Scenario Features

- 10 台摆轮，中间长度不一致 (800mm - 1500mm)
- 异常口在末端 (ChuteId=11)
- 每 300ms 创建包裹，默认 1000 个
- 单包裹从入口到异常口约 2 分钟
- 暴露 Prometheus metrics 端点: http://localhost:9091/metrics

### 验收指标 / Acceptance Metrics

| 指标 | 端点查询 | 验收要求 |
|------|---------|---------|
| 零错分 | `simulation_mis_sort_total` | = 0 |
| 总处理包裹 | `sorting_total_parcels` | = 创建包裹数 |
| 成功率 | 计算自 `simulation_parcel_total` | > 85% |
| P95 延迟 | `sorting_success_latency_seconds` | < 180s |

详细文档：[ACCEPTANCE_SCENARIOS.md](../ACCEPTANCE_SCENARIOS.md)

---

## Quick Start / 快速开始

### Production Deployment (No Docker) / 生产环境部署（无Docker）

1. **Install Prometheus**
   - Download the official tarball from [prometheus.io](https://prometheus.io/download/).
   - Extract to `/opt/prometheus` (or another directory) and create a dedicated user `prometheus`.
   - Copy `monitoring/prometheus/prometheus.yml` and `monitoring/prometheus/alerts.yml` to `/etc/prometheus/`.
2. **Install Grafana**
   - Follow the [Grafana OSS installation guide](https://grafana.com/docs/grafana/latest/setup-grafana/installation/) for your OS.
   - Copy `monitoring/grafana/provisioning` and `monitoring/grafana/dashboards` into Grafana's provisioning directory (e.g. `/etc/grafana/`).
3. **Start Services**
   - Create and enable systemd units (example names: `prometheus.service`, `grafana-server.service`).
   - Ensure the sorter 应用按照 README 中的“生产环境部署”章节启动并在 `http://<host>:5000` 暴露指标。
4. **Verify**
   - Prometheus targets: `http://<host>:9090/targets`
   - Grafana health: `systemctl status grafana-server` 或访问 `http://<host>:3000`。

> 📌 提示：执行 `DISABLE_DOCKER=1 ./validate-monitoring.sh` 可在无Docker环境下验证配置文件格式。

### Using Docker Compose (Development) / 使用Docker Compose（开发环境）

```bash
# From repository root
cd /path/to/ZakYip.WheelDiverterSorter

# Start all monitoring services
docker-compose -f docker-compose.monitoring.yml up -d

# Access the services
# Application: http://localhost:5000
# Prometheus:  http://localhost:9090
# Grafana:     http://localhost:3000 (admin/admin)
```

## Configuration Files / 配置文件

### prometheus/prometheus.yml

Main Prometheus configuration file that defines:
- Scrape configurations for the sorter application
- Global settings (scrape interval, timeout, etc.)
- Alert rule files to load

主Prometheus配置文件，定义：
- 分拣应用的抓取配置
- 全局设置（抓取间隔、超时等）
- 要加载的告警规则文件

### prometheus/alerts.yml

Alert rules for the sorter system including:
- High failure rate alerts
- Throughput monitoring
- Queue backlog detection
- Device health monitoring
- Performance degradation detection

分拣系统的告警规则，包括：
- 高失败率告警
- 吞吐量监控
- 队列积压检测
- 设备健康监控
- 性能下降检测

### grafana/provisioning/datasources/prometheus.yml

Grafana datasource configuration for automatic Prometheus setup.
Grafana数据源配置，用于自动设置Prometheus。

### grafana/provisioning/dashboards/dashboards.yml

Dashboard provisioning configuration that automatically loads dashboards on startup.
仪表板配置，在启动时自动加载仪表板。

### grafana/dashboards/wheel-diverter-sorter.json

Main Grafana dashboard with panels for:
- Sorting overview (success rate, throughput, active requests)
- Performance metrics (path generation/execution times)
- Queue monitoring (length, wait times)
- Device status (diverters, sensors, RuleEngine connections)

主Grafana仪表板，包含以下面板：
- 分拣概览（成功率、吞吐量、活跃请求）
- 性能指标（路径生成/执行时间）
- 队列监控（长度、等待时间）
- 设备状态（摆轮、传感器、RuleEngine连接）

## Customization / 自定义

### Changing Scrape Interval / 修改抓取间隔

Edit `prometheus/prometheus.yml`:
```yaml
global:
  scrape_interval: 15s  # Change this value
```

### Modifying Alert Rules / 修改告警规则

Edit `prometheus/alerts.yml` to add, remove, or modify alert rules.

### Customizing Dashboard / 自定义仪表板

1. Access Grafana at http://localhost:3000
2. Open the dashboard
3. Click the gear icon (⚙️) → "JSON Model"
4. Make changes
5. Save and export the JSON
6. Replace `grafana/dashboards/wheel-diverter-sorter.json`

## Metrics Reference / 指标参考

For a complete list of available metrics and their descriptions, see:
- [PROMETHEUS_GUIDE.md](../PROMETHEUS_GUIDE.md) - Full metrics documentation
- [PROMETHEUS_IMPLEMENTATION_SUMMARY.md](../PROMETHEUS_IMPLEMENTATION_SUMMARY.md) - Implementation details

完整的可用指标列表和描述，请参见：
- [PROMETHEUS_GUIDE.md](../PROMETHEUS_GUIDE.md) - 完整指标文档
- [PROMETHEUS_IMPLEMENTATION_SUMMARY.md](../PROMETHEUS_IMPLEMENTATION_SUMMARY.md) - 实现详情

## Troubleshooting / 故障排查

### Prometheus not scraping metrics / Prometheus未抓取指标

1. Check Prometheus targets: http://localhost:9090/targets
2. Verify application is running: http://localhost:5000/metrics
3. Check network connectivity between containers

### Grafana shows no data / Grafana无数据

1. Verify Prometheus datasource: Configuration → Data Sources → Test
2. Check time range in dashboard (top right)
3. Ensure metrics exist by querying Prometheus directly

### Dashboard panels show errors / 仪表板面板显示错误

1. Check Prometheus logs: `docker-compose logs prometheus`
2. Verify PromQL queries are valid
3. Ensure required metrics are being collected

For more detailed troubleshooting, see [GRAFANA_DASHBOARD_GUIDE.md](../GRAFANA_DASHBOARD_GUIDE.md).

## Security Notes / 安全注意事项

⚠️ **Important for Production / 生产环境重要提示**:

1. Change default Grafana password (admin/admin)
2. Configure authentication for Prometheus if exposed externally
3. Use HTTPS in production environments
4. Restrict network access using firewalls or VPNs
5. Regularly update container images

## Support / 支持

For issues or questions:
- Check [GRAFANA_DASHBOARD_GUIDE.md](../GRAFANA_DASHBOARD_GUIDE.md)
- Review [PROMETHEUS_GUIDE.md](../PROMETHEUS_GUIDE.md)
- Open an issue on GitHub

如有问题或疑问：
- 查看 [GRAFANA_DASHBOARD_GUIDE.md](../GRAFANA_DASHBOARD_GUIDE.md)
- 查阅 [PROMETHEUS_GUIDE.md](../PROMETHEUS_GUIDE.md)
- 在GitHub上提交issue
