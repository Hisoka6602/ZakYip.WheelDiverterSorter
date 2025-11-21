# API Migration Guide

**Last Updated**: 2025-11-21  
**Applies To**: API Refactoring (PR: refactor-api-endpoints-structure)

## Overview

This guide documents API endpoint changes made to consolidate and standardize the API surface. All changes maintain backward compatibility where possible, but some duplicate and redirect endpoints have been removed to reduce confusion and maintenance burden.

## Removed Endpoints

### 1. Duplicate Sorting Mode Endpoints

**Removed**:
- `GET /api/config/sorting-mode`
- `PUT /api/config/sorting-mode`

**Reason**: These endpoints were duplicates of the canonical sorting-mode endpoints in SystemConfigController.

**Migration Path**:
```
OLD: GET /api/config/sorting-mode
NEW: GET /api/config/system/sorting-mode

OLD: PUT /api/config/sorting-mode
NEW: PUT /api/config/system/sorting-mode
```

**Request/Response Format**: Unchanged. The DTOs (`SortingModeRequest`, `SortingModeResponse`) remain the same.

**Example Migration**:
```javascript
// OLD CODE
const response = await fetch('/api/config/sorting-mode');

// NEW CODE
const response = await fetch('/api/config/system/sorting-mode');
```

### 2. Simulation Scenario Redirect Endpoints

**Removed**:
- `GET /api/config/simulation-scenario`
- `PUT /api/config/simulation-scenario`

**Reason**: These were redirect endpoints that only returned a message to use the actual endpoint. Removing unnecessary indirection.

**Migration Path**:
```
OLD: GET /api/config/simulation-scenario
NEW: GET /api/config/simulation

OLD: PUT /api/config/simulation-scenario
NEW: PUT /api/config/simulation
```

**Request/Response Format**: Unchanged. Use the canonical simulation config endpoints.

**Example Migration**:
```javascript
// OLD CODE
const response = await fetch('/api/config/simulation-scenario');

// NEW CODE
const response = await fetch('/api/config/simulation');
```

## Current API Structure

After these changes, the API surface is organized as follows:

### Configuration Module (`/api/config/**`)

| Endpoint | Method | Controller | Purpose |
|----------|--------|------------|---------|
| `/api/config/topology` | GET/PUT | ConfigurationController | 线体拓扑配置 |
| `/api/config/exception-policy` | GET/PUT | ConfigurationController | 异常路由策略 |
| `/api/config/release-throttle` | GET/PUT | ConfigurationController | 放包节流配置 |
| `/api/config/system` | GET/PUT/POST | SystemConfigController | 系统配置 |
| `/api/config/system/sorting-mode` | GET/PUT | SystemConfigController | 分拣模式 ⭐ |
| `/api/config/driver` | GET/PUT/POST | DriverConfigController | 驱动器配置 |
| `/api/config/sensor` | GET/PUT/POST | SensorConfigController | 传感器配置 |
| `/api/config/io-linkage` | GET/PUT/POST | IoLinkageController | IO联动配置 |
| `/api/config/routes` | GET/POST/PUT/DELETE | RouteConfigController | 路由配置 |
| `/api/config/simulation` | GET/PUT | SimulationConfigController | 仿真配置 ⭐ |
| `/api/config/overload-policy` | GET/PUT | OverloadPolicyController | 超载策略 |

⭐ = Canonical endpoints (duplicates removed)

### Health & Status Module

| Endpoint | Method | Controller | Purpose |
|----------|--------|------------|---------|
| `/healthz` | GET | HealthController | K8s liveness probe |
| `/health/live` | GET | HealthController | K8s liveness probe |
| `/health/startup` | GET | HealthController | K8s startup probe |
| `/health/ready` | GET | HealthController | K8s readiness probe |
| `/health/line` | GET | HealthController | 线体健康检查 |

### Control Module

| Endpoint | Method | Controller | Purpose |
|----------|--------|------------|---------|
| `/api/diverts/change-chute` | POST | DivertsController | 请求改口 |
| `/api/sim/panel/start` | POST | SimulationPanelController | 启动系统 |
| `/api/sim/panel/stop` | POST | SimulationPanelController | 停止系统 |
| `/api/sim/panel/emergency-stop` | POST | SimulationPanelController | 急停 |
| `/api/sim/panel/emergency-reset` | POST | SimulationPanelController | 急停复位 |

### Simulation Module

| Endpoint | Method | Controller | Purpose |
|----------|--------|------------|---------|
| `/api/simulation/run-scenario-e` | POST | SimulationController | 运行场景E |
| `/api/simulation/stop` | POST | SimulationController | 停止仿真 |
| `/api/simulation/status` | GET | SimulationController | 仿真状态 |
| `/api/simulation/panel/press` | POST | PanelSimulationController | 模拟按钮按下 |
| `/api/simulation/panel/release` | POST | PanelSimulationController | 模拟按钮释放 |
| `/api/simulation/panel/state` | GET | PanelSimulationController | 面板状态 |
| `/api/simulation/panel/reset` | POST | PanelSimulationController | 重置面板 |

### Communication Module

| Endpoint | Method | Controller | Purpose |
|----------|--------|------------|---------|
| `/api/communication/config` | GET | CommunicationController | 获取通信配置 |
| `/api/communication/test` | POST | CommunicationController | 测试连接 |
| `/api/communication/status` | GET | CommunicationController | 通信状态 |
| `/api/communication/reset-stats` | POST | CommunicationController | 重置统计 |
| `/api/communication/config/persisted` | GET/PUT/POST | CommunicationController | 持久化配置 |

### Alarms Module

| Endpoint | Method | Controller | Purpose |
|----------|--------|------------|---------|
| `/api/alarms` | GET | AlarmsController | 获取活跃告警 |
| `/api/alarms/sorting-failure-rate` | GET | AlarmsController | 分拣失败率 |
| `/api/alarms/acknowledge` | POST | AlarmsController | 确认告警 |
| `/api/alarms/reset-statistics` | POST | AlarmsController | 重置统计 |

## Breaking Changes

### None (So Far)

The changes made in this refactoring are **non-breaking** because:
1. We only removed duplicate endpoints, not primary/canonical ones
2. The canonical endpoints retain the same request/response formats
3. Clients using the canonical endpoints require no changes

### Future Deprecations (Planned)

The following endpoints may be deprecated/consolidated in future releases:
- Multiple configuration endpoints may be merged into fewer controllers
- `/api/communication` may move to `/api/config/communication` for consistency
- `/api/sim` vs `/api/simulation` inconsistency may be resolved

## Testing Your Migration

After migrating your code:

1. **Run Integration Tests**:
   ```bash
   dotnet test tests/ZakYip.WheelDiverterSorter.Host.IntegrationTests
   ```

2. **Run E2E Tests**:
   ```bash
   dotnet test tests/ZakYip.WheelDiverterSorter.E2ETests
   ```

3. **Manual Verification**:
   - Verify configuration endpoints return expected data
   - Verify system starts/stops correctly
   - Verify simulation scenarios run successfully

## Support

If you encounter issues during migration:
1. Check this guide for the correct endpoint mapping
2. Review the API inventory document: `docs/internal/API_INVENTORY.md`
3. Check Swagger documentation: `/swagger/index.html`
4. Consult the main API reference: `docs/API_REFERENCE.md` (to be updated)

## Changelog

### 2025-11-21
- Removed duplicate sorting-mode endpoints from ConfigurationController
- Removed simulation-scenario redirect endpoints from ConfigurationController
- Created API migration guide
- Created API inventory document
