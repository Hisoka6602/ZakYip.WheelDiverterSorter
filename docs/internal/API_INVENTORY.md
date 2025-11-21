# API Inventory - ZakYip.WheelDiverterSorter

**Generated**: 2025-11-21  
**Purpose**: Complete inventory of all public API endpoints before refactoring

## Summary Statistics

- **Total Controllers**: 16
- **Total Endpoints**: ~60+ (详细统计如下)
- **Route Prefixes**: 8 distinct patterns (需要整合)

## Endpoint Categories

### 1. Configuration Management (配置类)

#### 1.1 ConfigurationController (`/api/config`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/api/config/topology` | 获取线体拓扑配置 | LineTopologyConfig | 前端/脚本 |
| PUT | `/api/config/topology` | 更新线体拓扑配置 | LineTopologyConfig | 前端/脚本 |
| GET | `/api/config/sorting-mode` | 获取分拣模式 | SortingModeResponse | 前端/脚本 |
| PUT | `/api/config/sorting-mode` | 更新分拣模式 | SortingModeRequest | 前端/脚本 |
| GET | `/api/config/exception-policy` | 获取异常路由策略 | ExceptionRoutingPolicy | 前端/脚本 |
| PUT | `/api/config/exception-policy` | 更新异常路由策略 | ExceptionRoutingPolicy | 前端/脚本 |
| GET | `/api/config/simulation-scenario` | 获取仿真场景配置(重定向) | N/A | 待废弃 |
| PUT | `/api/config/simulation-scenario` | 更新仿真场景配置(重定向) | N/A | 待废弃 |
| GET | `/api/config/release-throttle` | 获取放包节流配置 | ReleaseThrottleConfigResponse | 前端/脚本 |
| PUT | `/api/config/release-throttle` | 更新放包节流配置 | ReleaseThrottleConfigRequest | 前端/脚本 |

**Notes**: 
- `simulation-scenario` endpoints redirect to other endpoints - candidates for removal
- Contains throttle config which overlaps with system config

#### 1.2 SystemConfigController (`/api/config/system`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/api/config/system` | 获取完整系统配置 | SystemConfiguration | 前端/脚本 |
| GET | `/api/config/system/template` | 获取配置模板 | SystemConfiguration | 前端/脚本 |
| PUT | `/api/config/system` | 更新完整系统配置 | SystemConfiguration | 前端/脚本 |
| POST | `/api/config/system/reset` | 重置为默认配置 | SystemConfiguration | 前端/脚本 |
| GET | `/api/config/system/sorting-mode` | 获取分拣模式(重复) | SortingModeResponse | 重复 |
| PUT | `/api/config/system/sorting-mode` | 更新分拣模式(重复) | SortingModeRequest | 重复 |

**Notes**:
- `sorting-mode` endpoints duplicate ConfigurationController functionality
- **Recommendation**: Keep system-level config here, remove duplicates

#### 1.3 DriverConfigController (`/api/config/driver`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/api/config/driver` | 获取驱动器配置 | DriverConfiguration | 前端/脚本 |
| PUT | `/api/config/driver` | 更新驱动器配置 | DriverConfiguration | 前端/脚本 |
| POST | `/api/config/driver/reset` | 重置驱动器配置 | DriverConfiguration | 前端/脚本 |

**Notes**: Good structure, keep as-is under `/api/config/driver`

#### 1.4 SensorConfigController (`/api/config/sensor`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/api/config/sensor` | 获取传感器配置 | SensorConfiguration | 前端/脚本 |
| PUT | `/api/config/sensor` | 更新传感器配置 | SensorConfiguration | 前端/脚本 |
| POST | `/api/config/sensor/reset` | 重置传感器配置 | SensorConfiguration | 前端/脚本 |

**Notes**: Good structure, keep as-is under `/api/config/sensor`

#### 1.5 IoLinkageController (`/api/config/io-linkage`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/api/config/io-linkage` | 获取IO联动配置 | IoLinkageConfigResponse | 前端/脚本 |
| PUT | `/api/config/io-linkage` | 更新IO联动配置 | IoLinkageConfigRequest | 前端/脚本 |
| POST | `/api/config/io-linkage/trigger` | 手动触发IO联动 | N/A | 测试/调试 |
| GET | `/api/config/io-linkage/status/{bitNumber}` | 查询IO点状态 | N/A | 诊断 |

**Notes**: Good structure, keep as-is. Consider moving trigger/status to control/diagnostics

#### 1.6 RouteConfigController (`/api/config/routes`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/api/config/routes` | 获取所有路由配置 | List<RouteConfig> | 前端/脚本 |
| GET | `/api/config/routes/{chuteId}` | 获取指定格口路由 | RouteConfig | 前端/脚本 |
| POST | `/api/config/routes` | 创建路由配置 | RouteConfig | 前端/脚本 |
| PUT | `/api/config/routes/{chuteId}` | 更新路由配置 | RouteConfig | 前端/脚本 |
| DELETE | `/api/config/routes/{chuteId}` | 删除路由配置 | N/A | 前端/脚本 |
| GET | `/api/config/routes/export` | 导出路由配置 | File | 前端/脚本 |
| POST | `/api/config/routes/import` | 导入路由配置 | File | 前端/脚本 |

**Notes**: RESTful design, good structure, keep as-is

#### 1.7 SimulationConfigController (`/api/config/simulation`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/api/config/simulation` | 获取仿真配置 | SimulationConfiguration | 前端/测试 |
| PUT | `/api/config/simulation` | 更新仿真配置 | SimulationConfiguration | 前端/测试 |

**Notes**: Good structure, keep as-is under config

#### 1.8 OverloadPolicyController (`/api/config`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/api/config/overload-policy` | 获取超载策略配置 | OverloadPolicyDto | 前端/脚本 |
| PUT | `/api/config/overload-policy` | 更新超载策略配置 | OverloadPolicyDto | 前端/脚本 |

**Notes**: Should be merged into SystemConfigController or ConfigurationController

#### 1.9 CommunicationController (`/api/communication`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/api/communication/config` | 获取通信配置 | RuleEngineConnectionOptions | 前端/脚本 |
| POST | `/api/communication/test` | 测试连接 | ConnectionTestResponse | 前端/脚本 |
| GET | `/api/communication/status` | 获取通信状态 | CommunicationStatusResponse | 前端/监控 |
| POST | `/api/communication/reset-stats` | 重置统计 | N/A | 测试/调试 |
| GET | `/api/communication/config/persisted` | 获取持久化配置 | CommunicationConfiguration | 前端/脚本 |
| PUT | `/api/communication/config/persisted` | 更新持久化配置 | CommunicationConfiguration | 前端/脚本 |
| POST | `/api/communication/config/persisted/reset` | 重置持久化配置 | CommunicationConfiguration | 前端/脚本 |

**Notes**: Should be renamed to `/api/config/communication` for consistency

---

### 2. Runtime Control (运行控制类)

#### 2.1 SimulationPanelController (`/api/sim/panel`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| POST | `/api/sim/panel/start` | 启动系统 | N/A | 前端/脚本 |
| POST | `/api/sim/panel/stop` | 停止系统 | N/A | 前端/脚本 |
| POST | `/api/sim/panel/emergency-stop` | 急停 | N/A | 前端/脚本 |
| POST | `/api/sim/panel/emergency-reset` | 急停复位 | N/A | 前端/脚本 |
| GET | `/api/sim/panel/state` | 获取面板状态 | N/A | 前端/脚本 |

**Notes**: 
- This is actually control, not simulation
- **Recommendation**: Move to `/api/control/system` or similar

#### 2.2 DivertsController (`/api/diverts`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| POST | `/api/diverts/change-chute` | 请求改口 | ChuteChangeRequest | 上游/前端 |

**Notes**: 
- This is a control operation
- **Recommendation**: Move to `/api/control/diverts` or keep as specialized endpoint

---

### 3. Status & Monitoring (状态查询类)

#### 3.1 HealthController (`/`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/healthz` | 进程级健康检查 | ProcessHealthResponse | K8s liveness |
| GET | `/health/live` | 进程级健康检查(别名) | ProcessHealthResponse | K8s liveness |
| GET | `/health/startup` | 启动检查 | ProcessHealthResponse | K8s startup |
| GET | `/health/ready` | 就绪检查 | LineHealthResponse | K8s readiness |
| GET | `/health/line` | 线体健康检查(向后兼容) | LineHealthResponse | 前端/监控 |

**Notes**: 
- Good K8s-aligned structure
- **Recommendation**: Keep as-is, consider grouping under `/api/status/health` for consistency, but root `/healthz` is standard

#### 3.2 SimulationRunnerController (`/api/sim`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/api/sim/status` | 获取仿真状态 | N/A | 前端/测试 |
| POST | `/api/sim/reset` | 重置仿真 | N/A | 测试 |

**Notes**: 
- Mixing status and control
- **Recommendation**: Split between `/api/status/simulation` and `/api/control/simulation`

#### 3.3 AlarmsController (`/api/alarms`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| GET | `/api/alarms` | 获取活跃告警 | List<AlarmEvent> | 前端/监控 |
| GET | `/api/alarms/sorting-failure-rate` | 获取分拣失败率 | N/A | 前端/监控 |
| POST | `/api/alarms/acknowledge` | 确认告警 | N/A | 前端 |
| POST | `/api/alarms/reset-statistics` | 重置统计 | N/A | 测试/调试 |

**Notes**: 
- Mixing status (GET) and control (POST)
- **Recommendation**: Split GET to `/api/status/alarms`, POST to `/api/control/alarms`

---

### 4. Simulation & Testing (仿真类)

#### 4.1 SimulationController (`/api/simulation`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| POST | `/api/simulation/run-scenario-e` | 运行场景E | N/A | E2E测试 |
| POST | `/api/simulation/stop` | 停止仿真 | N/A | E2E测试 |
| GET | `/api/simulation/status` | 获取仿真状态 | N/A | E2E测试 |

**Notes**: Good structure, keep under `/api/simulation`

#### 4.2 PanelSimulationController (`/api/simulation/panel`)
| Method | Path | Purpose | DTO | Used By |
|--------|------|---------|-----|---------|
| POST | `/api/simulation/panel/press` | 模拟按钮按下 | N/A | 测试 |
| POST | `/api/simulation/panel/release` | 模拟按钮释放 | N/A | 测试 |
| GET | `/api/simulation/panel/state` | 获取面板状态 | N/A | 测试 |
| POST | `/api/simulation/panel/reset` | 重置面板 | N/A | 测试 |
| GET | `/api/simulation/panel/signal-tower/history` | 获取信号塔历史 | N/A | 测试 |

**Notes**: Good structure, keep under `/api/simulation/panel`

---

## Issues Identified

### 1. Route Prefix Inconsistency
- `/api/config` - primary config route
- `/api/config/system` - system config
- `/api/config/driver` - driver config
- `/api/config/sensor` - sensor config
- `/api/config/io-linkage` - IO config
- `/api/config/routes` - route config
- `/api/config/simulation` - simulation config
- `/api/communication` - **Should be** `/api/config/communication`
- `/api/sim` vs `/api/simulation` - **Inconsistent naming**
- `/api/alarms` - Should split to `/api/status` and `/api/control`
- `/api/diverts` - Should move to `/api/control`

### 2. Duplicate Endpoints
- **Sorting Mode**: 
  - `GET/PUT /api/config/sorting-mode` (ConfigurationController)
  - `GET/PUT /api/config/system/sorting-mode` (SystemConfigController)
  - **Action**: Remove from SystemConfigController

- **Simulation Scenario**: 
  - `GET/PUT /api/config/simulation-scenario` (ConfigurationController) - redirects
  - Actual config at `/api/config/simulation`
  - **Action**: Remove redirect endpoints

### 3. Mixed Concerns
- **SimulationPanelController** (`/api/sim/panel`) contains system control, not simulation
  - Should be `/api/control/system`
- **AlarmsController** mixes status queries with control actions
  - Split to `/api/status/alarms` and `/api/control/alarms`
- **SimulationRunnerController** mixes status and control
  - Split appropriately

### 4. Non-Standard Patterns
- **CommunicationController** under `/api/communication` instead of `/api/config/communication`
- **HealthController** on root path (OK for K8s, but inconsistent)

---

## Recommended Target Structure

### Module 1: `/api/config/**` (Configuration)
- `/api/config/system` - System-level config (SystemConfigController)
- `/api/config/topology` - Topology config (merge into SystemConfigController or keep in ConfigurationController)
- `/api/config/driver` - Driver config (DriverConfigController) ✓
- `/api/config/sensor` - Sensor config (SensorConfigController) ✓
- `/api/config/io-linkage` - IO linkage (IoLinkageController) ✓
- `/api/config/routes` - Route config (RouteConfigController) ✓
- `/api/config/simulation` - Simulation config (SimulationConfigController) ✓
- `/api/config/communication` - Communication config (rename from `/api/communication`)
- `/api/config/sorting-mode` - Sorting mode (unified)
- `/api/config/exception-policy` - Exception policy (unified)
- `/api/config/overload-policy` - Overload policy (unified)
- `/api/config/release-throttle` - Throttle config (unified)

### Module 2: `/api/control/**` (Runtime Control)
- `/api/control/system/start` - Start system
- `/api/control/system/stop` - Stop system
- `/api/control/system/emergency-stop` - Emergency stop
- `/api/control/system/emergency-reset` - Emergency reset
- `/api/control/diverts/change-chute` - Chute change
- `/api/control/alarms/acknowledge` - Acknowledge alarm
- `/api/control/alarms/reset-statistics` - Reset alarm stats
- `/api/control/io-linkage/trigger` - Trigger IO linkage

### Module 3: `/api/status/**` (Status & Monitoring)
- `/health/live` - Liveness probe (keep on root for K8s)
- `/health/ready` - Readiness probe (keep on root for K8s)
- `/health/startup` - Startup probe (keep on root for K8s)
- `/healthz` - Alias for liveness (keep on root for K8s)
- `/api/status/system` - System state
- `/api/status/alarms` - Active alarms
- `/api/status/alarms/failure-rate` - Sorting failure rate
- `/api/status/communication` - Communication status
- `/api/status/simulation` - Simulation status

### Module 4: `/api/simulation/**` (Simulation)
- `/api/simulation/scenarios/run` - Run scenario (e.g., scenario-e)
- `/api/simulation/scenarios/stop` - Stop scenario
- `/api/simulation/panel/press` - Simulate button press
- `/api/simulation/panel/release` - Simulate button release
- `/api/simulation/panel/state` - Panel state
- `/api/simulation/panel/reset` - Reset panel
- `/api/simulation/panel/signal-tower/history` - Signal tower history

### Module 5: `/api/diagnostics/**` (Optional - Diagnostics)
- `/api/diagnostics/io-linkage/status/{bitNumber}` - IO point status
- `/api/diagnostics/communication/test` - Test connection
- `/api/diagnostics/communication/reset-stats` - Reset comm stats

---

## Next Steps

1. **Create New Consolidated Controllers**:
   - `SystemControlController` (`/api/control/system`)
   - `StatusController` or split into `SystemStatusController`, `AlarmStatusController`, etc.
   - Merge configuration endpoints into fewer controllers

2. **Update Existing Controllers**:
   - Rename `CommunicationController` route to `/api/config/communication`
   - Split `AlarmsController` 
   - Split `SimulationRunnerController`
   - Move `SimulationPanelController` system control to new location

3. **Remove Duplicate Endpoints**:
   - Remove sorting-mode from SystemConfigController
   - Remove simulation-scenario redirects from ConfigurationController
   - Merge OverloadPolicyController into SystemConfigController or ConfigurationController

4. **Standardize DTOs**:
   - Create `ApiResponse<T>` wrapper
   - Add validation attributes to all request DTOs
   - Ensure consistent error response format

5. **Update Tests**:
   - Update integration tests for new routes
   - Add tests for validation
   - Verify E2E tests still pass

6. **Documentation**:
   - Update Swagger grouping
   - Update API_REFERENCE.md
   - Add migration guide for clients
