# Prometheus集成指南 / Prometheus Integration Guide

本文档描述如何配置Prometheus以抓取摆轮分拣系统的指标。
This document describes how to configure Prometheus to scrape metrics from the Wheel Diverter Sorter system.

## 暴露的指标 / Exposed Metrics

系统在 `/metrics` 端点暴露以下Prometheus指标：
The system exposes the following Prometheus metrics at the `/metrics` endpoint:

### 分拣指标 / Sorting Metrics

- `sorter_sorting_success_total` - 分拣成功总数 / Total successful sortings
- `sorter_sorting_failure_total` - 分拣失败总数 / Total failed sortings
- `sorter_parcel_throughput_total` - 包裹处理总数 / Total parcels processed
- `sorter_sorting_duration_seconds` - 整体分拣耗时（直方图）/ Overall sorting duration (histogram)
- `sorter_active_requests` - 当前活跃的分拣请求数 / Current active sorting requests

**成功率计算 / Success Rate Calculation:**
```promql
rate(sorter_sorting_success_total[5m]) / 
(rate(sorter_sorting_success_total[5m]) + rate(sorter_sorting_failure_total[5m]))
```

**吞吐量（每分钟）/ Throughput (per minute):**
```promql
rate(sorter_parcel_throughput_total[1m]) * 60
```

### 路径生成和执行指标 / Path Generation and Execution Metrics

- `sorter_path_generation_duration_seconds` - 路径生成耗时（直方图）/ Path generation duration (histogram)
- `sorter_path_execution_duration_seconds` - 路径执行耗时（直方图）/ Path execution duration (histogram)

**平均路径生成时间 / Average Path Generation Time:**
```promql
rate(sorter_path_generation_duration_seconds_sum[5m]) /
rate(sorter_path_generation_duration_seconds_count[5m])
```

**P95路径执行时间 / P95 Path Execution Time:**
```promql
histogram_quantile(0.95, rate(sorter_path_execution_duration_seconds_bucket[5m]))
```

### 队列指标 / Queue Metrics

- `sorter_queue_length` - 当前队列长度 / Current queue length
- `sorter_queue_wait_time_seconds` - 队列等待时间（直方图）/ Queue wait time (histogram)

**平均队列等待时间 / Average Queue Wait Time:**
```promql
rate(sorter_queue_wait_time_seconds_sum[5m]) /
rate(sorter_queue_wait_time_seconds_count[5m])
```

### 摆轮指标 / Diverter Metrics

- `sorter_diverter_active_count{diverter_id}` - 摆轮活跃状态 / Diverter active status
- `sorter_diverter_operations_total{diverter_id,direction}` - 摆轮操作总数 / Total diverter operations
- `sorter_diverter_utilization_ratio{diverter_id}` - 摆轮使用率 / Diverter utilization ratio

**摆轮操作速率 / Diverter Operation Rate:**
```promql
rate(sorter_diverter_operations_total[5m])
```

### RuleEngine连接指标 / RuleEngine Connection Metrics

- `sorter_ruleengine_connection_status{connection_type}` - RuleEngine连接状态 / RuleEngine connection status (1=connected, 0=disconnected)
- `sorter_ruleengine_messages_sent_total{connection_type,message_type}` - 发送消息总数 / Total messages sent
- `sorter_ruleengine_messages_received_total{connection_type,message_type}` - 接收消息总数 / Total messages received

**消息发送速率 / Message Send Rate:**
```promql
rate(sorter_ruleengine_messages_sent_total[5m])
```

### 传感器指标 / Sensor Metrics

- `sorter_sensor_health_status{sensor_id,sensor_type}` - 传感器健康状态 / Sensor health status (1=healthy, 0=faulty)
- `sorter_sensor_errors_total{sensor_id,sensor_type}` - 传感器错误总数 / Total sensor errors
- `sorter_sensor_detections_total{sensor_id,sensor_type}` - 传感器检测总数 / Total sensor detections

**传感器错误率 / Sensor Error Rate:**
```promql
rate(sorter_sensor_errors_total[5m])
```

## Prometheus配置 / Prometheus Configuration

在Prometheus配置文件（`prometheus.yml`）中添加以下配置：
Add the following configuration to your Prometheus configuration file (`prometheus.yml`):

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'wheel-diverter-sorter'
    static_configs:
      - targets: ['localhost:5000']  # 根据实际部署调整 / Adjust according to actual deployment
    metrics_path: '/metrics'
    scrape_interval: 10s  # 抓取间隔，可根据需要调整 / Scrape interval, adjust as needed
```

### Docker部署示例 / Docker Deployment Example

如果使用Docker部署，配置示例：
If deploying with Docker, configuration example:

```yaml
scrape_configs:
  - job_name: 'wheel-diverter-sorter'
    static_configs:
      - targets: ['sorter-host:5000']  # 使用Docker服务名 / Use Docker service name
    metrics_path: '/metrics'
```

### Kubernetes部署示例 / Kubernetes Deployment Example

使用ServiceMonitor自动发现：
Use ServiceMonitor for automatic discovery:

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: wheel-diverter-sorter
  labels:
    app: wheel-diverter-sorter
spec:
  selector:
    matchLabels:
      app: wheel-diverter-sorter
  endpoints:
  - port: http
    path: /metrics
    interval: 10s
```

## 告警规则示例 / Alert Rules Example

推荐的告警规则（`alerts.yml`）：
Recommended alert rules (`alerts.yml`):

```yaml
groups:
  - name: sorter_alerts
    interval: 30s
    rules:
      # 高失败率告警 / High failure rate alert
      - alert: HighSortingFailureRate
        expr: |
          rate(sorter_sorting_failure_total[5m]) / 
          (rate(sorter_sorting_success_total[5m]) + rate(sorter_sorting_failure_total[5m])) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "高分拣失败率 / High sorting failure rate"
          description: "失败率超过10% / Failure rate exceeds 10%"

      # 吞吐量下降告警 / Throughput drop alert
      - alert: LowThroughput
        expr: rate(sorter_parcel_throughput_total[5m]) < 10
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "吞吐量过低 / Low throughput"
          description: "包裹吞吐量低于10个/分钟 / Parcel throughput below 10 per minute"

      # 队列积压告警 / Queue backlog alert
      - alert: HighQueueLength
        expr: sorter_queue_length > 100
        for: 2m
        labels:
          severity: warning
        annotations:
          summary: "队列积压 / Queue backlog"
          description: "队列长度超过100 / Queue length exceeds 100"

      # RuleEngine连接断开告警 / RuleEngine disconnection alert
      - alert: RuleEngineDisconnected
        expr: sorter_ruleengine_connection_status == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "RuleEngine连接断开 / RuleEngine disconnected"
          description: "RuleEngine连接类型 {{ $labels.connection_type }} 已断开 / RuleEngine connection type {{ $labels.connection_type }} is disconnected"

      # 传感器故障告警 / Sensor fault alert
      - alert: SensorFault
        expr: sorter_sensor_health_status == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "传感器故障 / Sensor fault"
          description: "传感器 {{ $labels.sensor_id }} ({{ $labels.sensor_type }}) 故障 / Sensor {{ $labels.sensor_id }} ({{ $labels.sensor_type }}) is faulty"

      # 高传感器错误率告警 / High sensor error rate alert
      - alert: HighSensorErrorRate
        expr: rate(sorter_sensor_errors_total[5m]) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "传感器错误率过高 / High sensor error rate"
          description: "传感器 {{ $labels.sensor_id }} 错误率过高 / Sensor {{ $labels.sensor_id }} error rate is too high"

      # 路径执行时间过长告警 / Long path execution time alert
      - alert: SlowPathExecution
        expr: |
          histogram_quantile(0.95, rate(sorter_path_execution_duration_seconds_bucket[5m])) > 5
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "路径执行缓慢 / Slow path execution"
          description: "P95路径执行时间超过5秒 / P95 path execution time exceeds 5 seconds"
```

## Grafana仪表板 / Grafana Dashboard

推荐的仪表板面板：
Recommended dashboard panels:

### 1. 分拣概览 / Sorting Overview
- 成功率（过去1小时）/ Success rate (past 1 hour)
- 吞吐量（实时）/ Throughput (real-time)
- 活跃请求数 / Active requests

### 2. 性能指标 / Performance Metrics
- 路径生成时间（P50, P95, P99）/ Path generation time (P50, P95, P99)
- 路径执行时间（P50, P95, P99）/ Path execution time (P50, P95, P99)
- 整体分拣时间（P50, P95, P99）/ Overall sorting time (P50, P95, P99)

### 3. 队列监控 / Queue Monitoring
- 队列长度（实时）/ Queue length (real-time)
- 平均等待时间 / Average wait time

### 4. 设备状态 / Device Status
- 摆轮使用率（热力图）/ Diverter utilization (heatmap)
- 传感器健康状态 / Sensor health status
- RuleEngine连接状态 / RuleEngine connection status

## 验证指标暴露 / Verify Metrics Exposure

使用curl测试指标端点：
Test metrics endpoint with curl:

```bash
curl http://localhost:5000/metrics
```

查看特定指标：
View specific metrics:

```bash
curl http://localhost:5000/metrics | grep sorter_
```

## 性能注意事项 / Performance Considerations

1. **抓取间隔**：建议10-15秒，避免过于频繁影响性能
   **Scrape interval**: Recommend 10-15 seconds, avoid too frequent scraping

2. **标签基数**：注意控制标签数量，避免高基数问题
   **Label cardinality**: Control label count to avoid high cardinality issues

3. **直方图桶**：已针对实际场景优化，如需调整请修改PrometheusMetrics.cs
   **Histogram buckets**: Already optimized for actual scenarios, modify PrometheusMetrics.cs if adjustment needed

## 故障排查 / Troubleshooting

### 指标未出现 / Metrics Not Appearing

1. 确认应用程序已启动并监听正确端口
   Confirm application is started and listening on correct port

2. 访问 `http://localhost:5000/metrics` 确认端点可用
   Visit `http://localhost:5000/metrics` to confirm endpoint is available

3. 指标在首次使用后才会出现，触发一些操作后重试
   Metrics appear after first use, trigger some operations and retry

### Prometheus抓取失败 / Prometheus Scrape Failure

1. 检查网络连接和防火墙设置
   Check network connection and firewall settings

2. 确认Prometheus配置中的target地址正确
   Confirm target address in Prometheus configuration is correct

3. 查看Prometheus日志获取详细错误信息
   Check Prometheus logs for detailed error information
