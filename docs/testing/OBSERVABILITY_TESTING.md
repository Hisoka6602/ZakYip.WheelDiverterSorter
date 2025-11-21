# Observability Testing Summary

## Overview
This document describes the observability testing implementation for the ZakYip WheelDiverterSorter system.

## Test Project
- **Project**: `ZakYip.WheelDiverterSorter.Observability.Tests`
- **Framework**: .NET 9.0
- **Test Framework**: xUnit
- **Total Tests**: 17 (all passing ✅)

## Health Check Endpoints (PR-34)

### Standardized Endpoints

The system implements standard Kubernetes health check endpoints:

#### 1. Liveness Probe - `/health/live` and `/healthz`
- **Purpose**: Checks if the process is alive and can respond to requests
- **Returns 200**: Process is alive
- **Returns 503**: Process is dead or unresponsive
- **Use case**: Container orchestration liveness probes

#### 2. Startup Probe - `/health/startup`
- **Purpose**: Checks if the application has completed initialization
- **Returns 200**: System has finished startup (not in Booting state)
- **Returns 503**: System is still starting up
- **Use case**: Container orchestration startup probes

#### 3. Readiness Probe - `/health/ready`
- **Purpose**: Checks if the system is ready to accept traffic
- **Returns 200**: All critical modules are healthy
- **Returns 503**: One or more critical modules are unhealthy
- **Checks**:
  - System state (Ready/Running)
  - Self-test results
  - RuleEngine connection status
  - Driver health status
  - TTL scheduler status (TODO)
  - Sensor health (via driver status)
- **Use case**: Container orchestration readiness probes, load balancer health checks

#### 4. Legacy Endpoint - `/health/line`
- **Purpose**: Backward compatibility
- **Behavior**: Redirects to `/health/ready`

### Health Check Metrics (PR-34)

The following Prometheus metrics are exposed for health monitoring:

- `health_check_status{check_type}` - Overall health status (1=healthy, 0=unhealthy)
  - Labels: `live`, `startup`, `ready`
- `ruleengine_connection_health{connection_type}` - RuleEngine connection health
- `ttl_scheduler_health` - TTL scheduler thread health
- `driver_health_status{driver_name}` - Individual driver health status
- `upstream_health_status{endpoint_name}` - Upstream system health status

### Alert Integration

Health status changes trigger alerts through the unified `IAlertSink` interface:
- `FileAlertSink` - Writes alerts to files
- `LogAlertSink` - Logs alerts
- `AlertHistoryService` - Maintains alert history for API queries

## Test Categories

### 1. 指标收集测试 (Metrics Collection Testing)

#### Performance Metrics Tests (性能指标测试)
Tests verify that the system correctly collects and records performance-related metrics:

- **Counter Metrics**: 
  - `sorter.requests.total` - Total sorting requests
  - `sorter.requests.success` - Successful sorting operations
  - `sorter.requests.failure` - Failed sorting operations
  - `sorter.path_generation.total` - Path generation attempts
  - `sorter.path_execution.total` - Path execution attempts

- **Histogram Metrics** (Duration Tracking):
  - `sorter.requests.duration` - Overall sorting duration
  - `sorter.path_generation.duration` - Path generation time
  - `sorter.path_execution.duration` - Path execution time

- **Gauge Metrics**:
  - `sorter.requests.active` - Currently active sorting requests

#### Business Metrics Tests (业务指标测试)
Tests validate business operation tracking:

- Success/Failure ratio tracking
- Operational workflow metrics (generation → execution → completion)
- Multi-operation scenarios

#### Test Files
- `SorterMetricsTests.cs` - 8 tests covering all metric types
- `MetricsTestHelper.cs` - Helper class for capturing metrics during tests

### 2. 日志记录测试 (Logging Testing)

#### Structured Logging Tests (结构化日志测试)
Tests ensure logs contain structured data for observability:

- **Structured Data Elements**:
  - ParcelId (包裹ID)
  - ChuteId (格口ID)
  - Duration metrics (时长指标): Total time, generation time, execution time
  - Failure reasons (失败原因)

#### Log Level Tests (日志级别测试)
Tests verify appropriate log levels for different scenarios:

- **Information Level** (`LogLevel.Information`):
  - Successful sorting operations
  - Contains: ParcelId, ChuteId, timing metrics
  
- **Warning Level** (`LogLevel.Warning`):
  - Path generation failures
  - Path execution failures
  - Contains: Failure reasons, partial timing data

- **Error Level** (`LogLevel.Error`):
  - Exceptions during sorting operations
  - Contains: Exception details, ParcelId, ChuteId

#### Test Files
- `StructuredLoggingTests.cs` - 9 tests covering all log levels and scenarios

## Test Execution

### Run All Observability Tests
```bash
dotnet test ZakYip.WheelDiverterSorter.Observability.Tests/ZakYip.WheelDiverterSorter.Observability.Tests.csproj
```

### Expected Output
```
Test summary: total: 17, failed: 0, succeeded: 17, skipped: 0
```

## Implementation Details

### Metrics Collection Testing
Uses `MeterListener` to capture metrics in real-time during test execution. The `MetricsTestHelper` class subscribes to the meter and collects:
- Long counters (request counts)
- Double histograms (duration measurements)
- Int gauges (active request tracking)

### Logging Testing
Uses Moq to mock `ILogger<T>` and verify:
- Log level usage
- Message content
- Structured data inclusion
- Exception handling

## Test Coverage

### Metrics Coverage
✅ Counter increments  
✅ Histogram value recording  
✅ Gauge value changes  
✅ Multiple metric types working together  
✅ Performance metric accuracy  
✅ Business metric tracking  

### Logging Coverage
✅ Information level logs  
✅ Warning level logs  
✅ Error level logs  
✅ Structured data inclusion  
✅ Timing metrics in logs  
✅ Failure reason logging  
✅ Exception details logging  

## Dependencies

- `Microsoft.Extensions.DependencyInjection` - For setting up metrics infrastructure
- `System.Diagnostics.Metrics` - For metrics collection
- `Microsoft.Extensions.Logging` - For logging infrastructure
- `Moq` - For mocking dependencies
- `xUnit` - Test framework

## Integration

The observability tests verify the functionality of:
- `SorterMetrics` class in `ZakYip.WheelDiverterSorter.Host`
- `OptimizedSortingService` class in `ZakYip.WheelDiverterSorter.Host`

These components provide production-ready observability features for monitoring the sorter system in real-time.

## Next Steps

To extend observability testing:
1. Add tests for additional metrics (if new metrics are added)
2. Add tests for distributed tracing (when OpenTelemetry is integrated)
3. Add performance benchmarks for metrics overhead
4. Add integration tests with actual telemetry exporters

## Security

✅ No security vulnerabilities detected (CodeQL scan passed)
