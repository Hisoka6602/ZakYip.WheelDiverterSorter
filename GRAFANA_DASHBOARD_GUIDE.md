# Grafana监控仪表板设置指南 / Grafana Dashboard Setup Guide

本文档描述如何设置和使用Grafana监控仪表板来可视化摆轮分拣系统的运行状态。
This document describes how to set up and use the Grafana monitoring dashboard to visualize the Wheel Diverter Sorter system's operational status.

## 目录 / Table of Contents

- [快速开始 / Quick Start](#快速开始--quick-start)
- [架构概述 / Architecture Overview](#架构概述--architecture-overview)
- [详细设置步骤 / Detailed Setup Steps](#详细设置步骤--detailed-setup-steps)
- [仪表板功能 / Dashboard Features](#仪表板功能--dashboard-features)
- [告警配置 / Alert Configuration](#告警配置--alert-configuration)
- [故障排查 / Troubleshooting](#故障排查--troubleshooting)

## 快速开始 / Quick Start

### 使用Docker Compose部署 / Deploy with Docker Compose

最简单的方式是使用Docker Compose一键部署整个监控栈：
The easiest way is to deploy the entire monitoring stack with Docker Compose:

```bash
# 克隆仓库（如果尚未克隆）
# Clone repository (if not already cloned)
git clone https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter.git
cd ZakYip.WheelDiverterSorter

# 启动所有服务
# Start all services
docker-compose -f docker-compose.monitoring.yml up -d

# 查看日志
# View logs
docker-compose -f docker-compose.monitoring.yml logs -f
```

服务将在以下端口启动：
Services will start on the following ports:

- **应用程序 / Application**: http://localhost:5000
  - Swagger UI: http://localhost:5000/swagger
  - Metrics端点: http://localhost:5000/metrics
- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3000

### 访问Grafana / Access Grafana

1. 打开浏览器，访问 http://localhost:3000
2. 使用默认凭据登录：
   - 用户名 / Username: `admin`
   - 密码 / Password: `admin`
3. 首次登录时会提示修改密码（可选）
4. 仪表板会自动加载，导航到 "Dashboards" → "Wheel Diverter Sorter Dashboard"

## 架构概述 / Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    监控架构 / Monitoring Architecture          │
└─────────────────────────────────────────────────────────────┘

┌──────────────────┐         ┌──────────────────┐
│                  │         │                  │
│  Sorter App      │ ──────▶ │   Prometheus     │
│  (Port 5000)     │  /metrics│  (Port 9090)     │
│                  │         │                  │
└──────────────────┘         └──────────────────┘
                                      │
                                      │ Query
                                      ▼
                             ┌──────────────────┐
                             │                  │
                             │    Grafana       │
                             │   (Port 3000)    │
                             │                  │
                             └──────────────────┘
```

### 组件说明 / Components

1. **摆轮分拣应用 / Sorter Application**
   - 暴露 `/metrics` 端点
   - 收集和暴露业务指标

2. **Prometheus**
   - 定期抓取应用的指标（默认15秒）
   - 存储时间序列数据（默认保留30天）
   - 评估告警规则

3. **Grafana**
   - 连接到Prometheus数据源
   - 可视化指标数据
   - 提供交互式仪表板

## 详细设置步骤 / Detailed Setup Steps

### 方式1：Docker Compose（推荐）/ Method 1: Docker Compose (Recommended)

#### 前置要求 / Prerequisites

- Docker 20.10+
- Docker Compose 1.29+
- 至少2GB可用内存

#### 步骤 / Steps

1. **配置文件检查 / Configuration Check**

   确认以下文件存在：
   Ensure the following files exist:
   
   ```
   monitoring/
   ├── prometheus/
   │   ├── prometheus.yml      # Prometheus配置
   │   └── alerts.yml          # 告警规则
   ├── grafana/
   │   ├── provisioning/
   │   │   ├── datasources/
   │   │   │   └── prometheus.yml  # 数据源配置
   │   │   └── dashboards/
   │   │       └── dashboards.yml  # 仪表板提供配置
   │   └── dashboards/
   │       └── wheel-diverter-sorter.json  # 仪表板定义
   docker-compose.monitoring.yml
   ```

2. **启动服务 / Start Services**

   ```bash
   docker-compose -f docker-compose.monitoring.yml up -d
   ```

3. **验证服务状态 / Verify Service Status**

   ```bash
   # 检查所有服务是否运行
   # Check if all services are running
   docker-compose -f docker-compose.monitoring.yml ps
   
   # 查看应用日志
   # View application logs
   docker-compose -f docker-compose.monitoring.yml logs sorter-app
   
   # 查看Prometheus日志
   # View Prometheus logs
   docker-compose -f docker-compose.monitoring.yml logs prometheus
   ```

4. **访问各个服务 / Access Services**

   - 应用Swagger UI: http://localhost:5000/swagger
   - 指标端点: http://localhost:5000/metrics
   - Prometheus: http://localhost:9090
   - Grafana: http://localhost:3000

### 方式2：手动部署 / Method 2: Manual Deployment

如果不使用Docker，可以手动部署各个组件：
If not using Docker, you can manually deploy each component:

#### 1. 部署应用程序 / Deploy Application

```bash
cd ZakYip.WheelDiverterSorter.Host
dotnet publish -c Release -o ./publish
cd publish
dotnet ZakYip.WheelDiverterSorter.Host.dll
```

#### 2. 安装和配置Prometheus / Install and Configure Prometheus

```bash
# 下载Prometheus
# Download Prometheus
wget https://github.com/prometheus/prometheus/releases/download/v2.45.0/prometheus-2.45.0.linux-amd64.tar.gz
tar xvfz prometheus-*.tar.gz
cd prometheus-*

# 复制配置文件
# Copy configuration files
cp /path/to/monitoring/prometheus/prometheus.yml .
cp /path/to/monitoring/prometheus/alerts.yml .

# 修改prometheus.yml中的目标地址
# Update target address in prometheus.yml
# 将 'sorter-app:5000' 改为 'localhost:5000'
# Change 'sorter-app:5000' to 'localhost:5000'

# 启动Prometheus
# Start Prometheus
./prometheus --config.file=prometheus.yml
```

#### 3. 安装和配置Grafana / Install and Configure Grafana

**在Linux上 / On Linux:**

```bash
# Ubuntu/Debian
sudo apt-get install -y software-properties-common
sudo add-apt-repository "deb https://packages.grafana.com/oss/deb stable main"
wget -q -O - https://packages.grafana.com/gpg.key | sudo apt-key add -
sudo apt-get update
sudo apt-get install grafana

# 启动Grafana
# Start Grafana
sudo systemctl start grafana-server
sudo systemctl enable grafana-server
```

**在macOS上 / On macOS:**

```bash
brew install grafana
brew services start grafana
```

**在Windows上 / On Windows:**

从 https://grafana.com/grafana/download 下载安装程序
Download installer from https://grafana.com/grafana/download

#### 4. 配置Grafana / Configure Grafana

1. 访问 http://localhost:3000
2. 登录（默认用户名和密码都是 `admin`）
3. 添加Prometheus数据源：
   - 点击 "Configuration" → "Data Sources" → "Add data source"
   - 选择 "Prometheus"
   - URL: `http://localhost:9090`
   - 点击 "Save & Test"
4. 导入仪表板：
   - 点击 "Dashboards" → "Import"
   - 上传 `monitoring/grafana/dashboards/wheel-diverter-sorter.json`
   - 选择Prometheus数据源
   - 点击 "Import"

## 仪表板功能 / Dashboard Features

仪表板分为以下几个主要部分：
The dashboard is divided into the following main sections:

### 1. 分拣概览 / Sorting Overview

展示系统的整体运行状态：
Displays overall system operational status:

- **分拣成功率** / **Success Rate**: 最近5分钟的成功率，使用仪表盘显示
  - 绿色 (>95%): 系统运行正常
  - 黄色 (90-95%): 需要关注
  - 红色 (<90%): 需要立即处理

- **包裹吞吐量** / **Parcel Throughput**: 每分钟处理的包裹数量，实时趋势图
  - 显示系统的处理能力
  - 帮助识别高峰和低谷时段

- **活跃请求数** / **Active Requests**: 当前正在处理的分拣请求数量
  - 绿色 (<10): 负载正常
  - 黄色 (10-50): 负载较高
  - 红色 (>50): 负载过高

- **分拣成功/失败趋势** / **Success/Failure Trend**: 堆叠面积图显示成功和失败的趋势

### 2. 性能指标 / Performance Metrics

展示系统各阶段的性能：
Shows performance at each stage of the system:

- **路径生成时间** / **Path Generation Time**: P50/P95/P99百分位数
  - 正常值: P95 < 100ms
  - 需要优化: P95 > 500ms

- **路径执行时间** / **Path Execution Time**: P50/P95/P99百分位数
  - 正常值: P95 < 2s
  - 需要优化: P95 > 5s

- **整体分拣时间** / **Overall Sorting Time**: 从请求到完成的总时间
  - 包含路径生成和执行的总和

### 3. 队列监控 / Queue Monitoring

监控队列状态和等待时间：
Monitors queue status and wait times:

- **队列长度** / **Queue Length**: 当前等待处理的请求数量
  - 绿色 (<50): 正常
  - 黄色 (50-100): 需要关注
  - 红色 (>100): 队列积压

- **队列等待时间** / **Queue Wait Time**: 平均等待时间和P95
  - 帮助识别瓶颈
  - 指导容量规划

### 4. 设备状态 / Device Status

监控各个硬件设备的状态：
Monitors the status of hardware devices:

- **RuleEngine连接状态** / **RuleEngine Connection Status**: 表格显示各连接类型的状态
  - 绿色: 已连接
  - 红色: 已断开

- **传感器健康状态** / **Sensor Health Status**: 表格显示所有传感器的健康状况
  - 绿色: 健康
  - 红色: 故障

- **摆轮使用率** / **Diverter Utilization**: 横向条形图显示各摆轮的使用率
  - 绿色 (<70%): 使用率正常
  - 黄色 (70-90%): 使用率较高
  - 红色 (>90%): 接近满负荷

- **摆轮操作速率** / **Diverter Operation Rate**: 各摆轮的操作频率
  - 按方向（左/右/直行）分组显示

- **RuleEngine消息速率** / **RuleEngine Message Rate**: 发送和接收消息的速率
  - 帮助诊断通信问题

### 仪表板设置 / Dashboard Settings

- **刷新间隔** / **Refresh Interval**: 10秒（可自定义）
- **时间范围** / **Time Range**: 默认显示最近1小时（可调整）
- **自动刷新** / **Auto Refresh**: 启用
- **主题** / **Theme**: 深色（可切换）

## 告警配置 / Alert Configuration

系统预配置了以下告警规则：
The system is pre-configured with the following alert rules:

### 告警级别 / Alert Levels

- **Critical（严重）**: 需要立即处理的问题
  - RuleEngine连接断开
  - 传感器故障
  - Prometheus目标不可达

- **Warning（警告）**: 需要关注但不紧急的问题
  - 高失败率
  - 低吞吐量
  - 队列积压
  - 性能下降

- **Info（信息）**: 仅供参考的通知
  - 系统空闲

### 告警规则详情 / Alert Rule Details

| 告警名称 / Alert Name | 触发条件 / Trigger Condition | 持续时间 / Duration | 级别 / Severity |
|----------------------|---------------------------|-------------------|----------------|
| HighSortingFailureRate | 失败率 > 10% | 5分钟 | Warning |
| LowThroughput | 吞吐量 < 10/分钟 | 5分钟 | Warning |
| HighQueueLength | 队列长度 > 100 | 2分钟 | Warning |
| HighQueueWaitTime | 等待时间 > 10秒 | 5分钟 | Warning |
| RuleEngineDisconnected | 连接断开 | 1分钟 | Critical |
| SensorFault | 传感器故障 | 1分钟 | Critical |
| HighSensorErrorRate | 错误率 > 0.1/秒 | 5分钟 | Warning |
| SlowPathExecution | P95 > 5秒 | 5分钟 | Warning |
| SlowPathGeneration | P95 > 1秒 | 5分钟 | Warning |
| NoActiveRequests | 无活跃请求 | 10分钟 | Info |
| HighDiverterUtilization | 使用率 > 90% | 5分钟 | Warning |

### 查看告警 / View Alerts

在Prometheus中查看告警：
View alerts in Prometheus:

```
http://localhost:9090/alerts
```

### 配置告警通知（可选）/ Configure Alert Notifications (Optional)

如需通过邮件、Slack等方式接收告警通知，可以配置AlertManager：
To receive alert notifications via email, Slack, etc., configure AlertManager:

1. 取消docker-compose.monitoring.yml中alertmanager部分的注释
2. 创建alertmanager配置文件
3. 重启服务

示例alertmanager配置：
Sample alertmanager configuration:

```yaml
# monitoring/alertmanager/config.yml
global:
  resolve_timeout: 5m
  smtp_smarthost: 'smtp.gmail.com:587'
  smtp_from: 'alerts@example.com'
  smtp_auth_username: 'alerts@example.com'
  smtp_auth_password: 'password'

route:
  group_by: ['alertname', 'severity']
  group_wait: 10s
  group_interval: 10s
  repeat_interval: 12h
  receiver: 'email'

receivers:
  - name: 'email'
    email_configs:
      - to: 'team@example.com'
        headers:
          Subject: '摆轮分拣系统告警 / Sorter System Alert'
```

## 故障排查 / Troubleshooting

### 问题1：无法访问Grafana / Issue 1: Cannot Access Grafana

**症状** / **Symptoms**: 浏览器无法打开 http://localhost:3000

**解决方案** / **Solutions**:

1. 检查Grafana容器是否运行：
   ```bash
   docker-compose -f docker-compose.monitoring.yml ps grafana
   ```

2. 检查端口是否被占用：
   ```bash
   # Linux/Mac
   sudo netstat -tlnp | grep 3000
   
   # Windows
   netstat -ano | findstr 3000
   ```

3. 查看Grafana日志：
   ```bash
   docker-compose -f docker-compose.monitoring.yml logs grafana
   ```

### 问题2：仪表板无数据 / Issue 2: Dashboard Shows No Data

**症状** / **Symptoms**: 仪表板面板显示 "No data"

**解决方案** / **Solutions**:

1. 检查应用是否运行并暴露指标：
   ```bash
   curl http://localhost:5000/metrics
   ```

2. 检查Prometheus是否能抓取指标：
   - 访问 http://localhost:9090/targets
   - 确认 "wheel-diverter-sorter" 目标状态为 "UP"

3. 检查Prometheus数据源配置：
   - Grafana → Configuration → Data Sources
   - 点击 "Test" 按钮确认连接

4. 触发一些分拣操作以生成指标：
   ```bash
   curl -X POST http://localhost:5000/api/debug/sort \
     -H "Content-Type: application/json" \
     -d '{"parcelId": "TEST001", "targetChuteId": 1}'
   ```

### 问题3：指标数据不准确 / Issue 3: Metric Data is Inaccurate

**症状** / **Symptoms**: 显示的数据与实际不符

**解决方案** / **Solutions**:

1. 检查时间同步：
   ```bash
   # 在容器中检查时间
   docker exec sorter-app date
   docker exec prometheus date
   ```

2. 调整时间范围：
   - 在Grafana右上角选择不同的时间范围
   - 尝试最近5分钟或15分钟

3. 刷新数据：
   - 点击Grafana右上角的刷新按钮
   - 或等待自动刷新（10秒）

### 问题4：告警未触发 / Issue 4: Alerts Not Firing

**症状** / **Symptoms**: 满足条件但告警未触发

**解决方案** / **Solutions**:

1. 检查Prometheus告警规则：
   - 访问 http://localhost:9090/rules
   - 确认规则已加载

2. 查看告警状态：
   - 访问 http://localhost:9090/alerts
   - 检查 "Pending" 和 "Firing" 状态

3. 验证告警规则语法：
   ```bash
   # 使用promtool验证
   docker exec prometheus promtool check rules /etc/prometheus/alerts.yml
   ```

### 问题5：Docker容器频繁重启 / Issue 5: Docker Containers Keep Restarting

**症状** / **Symptoms**: 容器不断重启

**解决方案** / **Solutions**:

1. 检查容器日志：
   ```bash
   docker-compose -f docker-compose.monitoring.yml logs --tail=100
   ```

2. 检查资源限制：
   ```bash
   docker stats
   ```

3. 增加内存限制（如果需要）：
   在docker-compose.monitoring.yml中添加：
   ```yaml
   services:
     sorter-app:
       deploy:
         resources:
           limits:
             memory: 1G
   ```

### 问题6：无法构建应用镜像 / Issue 6: Cannot Build Application Image

**症状** / **Symptoms**: Docker构建失败

**解决方案** / **Solutions**:

1. 检查Dockerfile路径：
   ```bash
   ls -la ZakYip.WheelDiverterSorter.Host/Dockerfile
   ```

2. 手动构建测试：
   ```bash
   docker build -f ZakYip.WheelDiverterSorter.Host/Dockerfile -t sorter-app .
   ```

3. 如果构建失败，检查.NET SDK是否可用：
   ```bash
   docker pull mcr.microsoft.com/dotnet/sdk:8.0
   ```

## 性能调优 / Performance Tuning

### Prometheus性能优化 / Prometheus Performance Optimization

1. **调整抓取间隔** / **Adjust Scrape Interval**:
   ```yaml
   # prometheus.yml
   global:
     scrape_interval: 30s  # 从15s增加到30s
   ```

2. **减少保留时间** / **Reduce Retention Time**:
   ```yaml
   # docker-compose.monitoring.yml
   prometheus:
     command:
       - '--storage.tsdb.retention.time=7d'  # 从30d减少到7d
   ```

3. **启用压缩** / **Enable Compression**:
   Prometheus自动使用压缩，无需额外配置

### Grafana性能优化 / Grafana Performance Optimization

1. **调整查询超时** / **Adjust Query Timeout**:
   ```yaml
   # grafana/provisioning/datasources/prometheus.yml
   jsonData:
     queryTimeout: 30s  # 从60s减少到30s
   ```

2. **限制数据点数量** / **Limit Data Points**:
   在仪表板面板设置中调整 "Max data points"

3. **使用更长的时间间隔** / **Use Longer Time Intervals**:
   在查询中使用 `[5m]` 而不是 `[1m]`

## 备份和恢复 / Backup and Recovery

### 备份数据 / Backup Data

```bash
# 备份Prometheus数据
# Backup Prometheus data
docker run --rm -v zakyipwheeldivertersorter_prometheus-data:/data \
  -v $(pwd)/backup:/backup alpine \
  tar czf /backup/prometheus-backup-$(date +%Y%m%d).tar.gz -C /data .

# 备份Grafana数据
# Backup Grafana data
docker run --rm -v zakyipwheeldivertersorter_grafana-data:/data \
  -v $(pwd)/backup:/backup alpine \
  tar czf /backup/grafana-backup-$(date +%Y%m%d).tar.gz -C /data .
```

### 恢复数据 / Restore Data

```bash
# 停止服务
# Stop services
docker-compose -f docker-compose.monitoring.yml down

# 恢复Prometheus数据
# Restore Prometheus data
docker run --rm -v zakyipwheeldivertersorter_prometheus-data:/data \
  -v $(pwd)/backup:/backup alpine \
  tar xzf /backup/prometheus-backup-YYYYMMDD.tar.gz -C /data

# 恢复Grafana数据
# Restore Grafana data
docker run --rm -v zakyipwheeldivertersorter_grafana-data:/data \
  -v $(pwd)/backup:/backup alpine \
  tar xzf /backup/grafana-backup-YYYYMMDD.tar.gz -C /data

# 重启服务
# Restart services
docker-compose -f docker-compose.monitoring.yml up -d
```

## 安全建议 / Security Recommendations

1. **修改默认密码** / **Change Default Passwords**:
   - Grafana管理员密码
   - 考虑为Prometheus添加认证

2. **使用HTTPS** / **Use HTTPS**:
   - 在生产环境中配置SSL/TLS
   - 使用反向代理（如Nginx）

3. **限制网络访问** / **Restrict Network Access**:
   - 使用防火墙限制端口访问
   - 考虑使用VPN或内网访问

4. **定期更新** / **Regular Updates**:
   ```bash
   # 更新镜像
   docker-compose -f docker-compose.monitoring.yml pull
   docker-compose -f docker-compose.monitoring.yml up -d
   ```

## 生产环境部署建议 / Production Deployment Recommendations

1. **使用持久化存储** / **Use Persistent Storage**:
   - 将数据卷映射到可靠的存储
   - 配置定期备份

2. **配置资源限制** / **Configure Resource Limits**:
   - CPU和内存限制
   - 根据负载调整

3. **实现高可用性** / **Implement High Availability**:
   - 多实例部署
   - 负载均衡

4. **监控监控系统** / **Monitor the Monitoring System**:
   - 为Prometheus和Grafana配置健康检查
   - 设置告警通知

## 更多资源 / Additional Resources

- [Prometheus官方文档](https://prometheus.io/docs/)
- [Grafana官方文档](https://grafana.com/docs/)
- [PromQL查询语言](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [Grafana仪表板最佳实践](https://grafana.com/docs/grafana/latest/dashboards/build-dashboards/best-practices/)

## 获取帮助 / Getting Help

如果遇到问题，可以：
If you encounter issues, you can:

1. 查看项目的GitHub Issues
2. 阅读PROMETHEUS_GUIDE.md了解指标详情
3. 查看系统日志进行故障排查

---

**版本** / **Version**: 1.0  
**最后更新** / **Last Updated**: 2024-01-15
