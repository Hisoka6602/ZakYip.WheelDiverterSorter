# PR-06 Implementation Summary: 配置管理 API 闭环 + 按配置驱动长跑仿真

## Overview
This PR implements a complete configuration management API system and integrates simulation scenario E with the system state machine, enabling API-driven long-run simulations.

## Implemented Features

### 1. Configuration Management API (`ConfigurationController`)

#### Topology Configuration
- **GET /api/config/topology** - Retrieve current line topology configuration
- **PUT /api/config/topology** - Update topology (returns 501 Not Implemented - requires file-based config)

#### Sorting Mode Configuration
- **GET /api/config/sorting-mode** - Get current sorting mode (Formal/FixedChute/RoundRobin)
- **PUT /api/config/sorting-mode** - Update sorting mode with validation

#### Exception Policy Configuration
- **GET /api/config/exception-policy** - Get current exception routing policy
- **PUT /api/config/exception-policy** - Update exception policy with validation

#### Simulation Scenario Configuration
- **GET /api/config/simulation-scenario** - Redirects to existing `/api/config/simulation`
- **PUT /api/config/simulation-scenario** - Redirects to existing `/api/config/simulation`

### 2. Simulation Controller (`SimulationController`)

#### Run Scenario E
- **POST /api/simulation/run-scenario-e** - Start scenario E long-run simulation
  - Checks system state must be Ready
  - Transitions state to Running
  - Runs simulation asynchronously
  - Returns state to Ready when complete

#### Stop Simulation
- **POST /api/simulation/stop** - Stop running simulation

#### Get Status
- **GET /api/simulation/status** - Get current simulation status

### 3. Simulation Integration

#### SimulationScenarioRunner
- Implements `ISimulationScenarioRunner` interface
- Reads configuration from:
  - `ILineTopologyConfigProvider` for topology
  - `SimulationOptions` for simulation parameters
- Supports runtime configuration updates via `SetRuntimeOptions()`
- Integrates with existing `SimulationRunner` for actual simulation execution

#### Configuration Support
- Added `IsSimulationEnabled` flag to `SimulationOptions`
- Configuration sync between `SimulationConfigController` and `SimulationScenarioRunner`
- Runtime configuration takes precedence over appsettings.json

### 4. Dependency Injection Setup

#### Program.cs Registration
```csharp
// Topology provider registration
builder.Services.AddSingleton<ILineTopologyConfigProvider>(/* JSON or default provider */);

// Simulation services (when EnableApiSimulation=true)
builder.Services.AddSingleton<SimulationRunner>();
builder.Services.AddSingleton<SimulationScenarioRunner>();
builder.Services.AddSingleton<ISimulationScenarioRunner>(/* maps to SimulationScenarioRunner */);

// Register runner with controller after app build
SimulationController.RegisterScenarioRunner(scenarioRunner);
```

#### Configuration in appsettings.json
```json
{
  "Simulation": {
    "EnableApiSimulation": true
  },
  "TopologyConfiguration": {
    "FilePath": null  // null = use default topology
  }
}
```

## Usage Examples

### 1. Configure and Run Simulation via API

```bash
# Step 1: Update simulation configuration
curl -X PUT http://localhost:5000/api/config/simulation \
  -H "Content-Type: application/json" \
  -d '{
    "parcelCount": 1000,
    "lineSpeedMmps": 1000,
    "parcelIntervalMs": 300,
    "sortingMode": "RoundRobin",
    "exceptionChuteId": 11,
    "isSimulationEnabled": true
  }'

# Step 2: Update exception policy
curl -X PUT http://localhost:5000/api/config/exception-policy \
  -H "Content-Type: application/json" \
  -d '{
    "exceptionChuteId": 11,
    "upstreamTimeoutMs": 10000,
    "retryOnTimeout": true,
    "retryCount": 3,
    "retryDelayMs": 1000
  }'

# Step 3: Run scenario E
curl -X POST http://localhost:5000/api/simulation/run-scenario-e

# Step 4: Monitor via Prometheus
# Access http://localhost:5000/metrics
```

### 2. Get Current Configuration

```bash
# Get topology
curl http://localhost:5000/api/config/topology

# Get sorting mode
curl http://localhost:5000/api/config/sorting-mode

# Get exception policy
curl http://localhost:5000/api/config/exception-policy

# Get simulation config
curl http://localhost:5000/api/config/simulation
```

### 3. Check Simulation Status

```bash
curl http://localhost:5000/api/simulation/status
```

## Panel Button Integration (Manual)

To integrate panel button with simulation mode:

### Option 1: Check in Panel Button Handler
When the Start button is pressed:
```csharp
// In panel button event handler
var simulationOptions = /* get from configuration */;
if (simulationOptions.IsSimulationEnabled)
{
    // Trigger simulation via HTTP call to /api/simulation/run-scenario-e
    // OR inject ISimulationScenarioRunner and call RunScenarioEAsync()
}
else
{
    // Normal machine start logic
}
```

### Option 2: Use Panel Simulation Controller
```bash
# Simulate pressing start button (if in simulation mode)
curl -X POST "http://localhost:5000/api/simulation/panel/press?buttonType=Start"
```

## State Machine Integration

The simulation properly integrates with the system state machine:

1. **Before Start**: State must be `Ready`
2. **On Start**: Transitions to `Running`
3. **During Simulation**: State remains `Running`
4. **On Complete/Cancel**: Returns to `Ready`
5. **On Error**: Attempts to return to `Ready`

## Validation

All configuration updates include validation:
- Sorting mode: validates fixed chute ID or available chute IDs based on mode
- Exception policy: validates exception chute ID, timeouts, retry counts
- Simulation config: validates parcel count, speeds, intervals, etc.

All error messages are in Chinese for consistency.

## Testing Recommendations

1. **API Testing**:
   - Use Swagger UI at `/swagger`
   - Test all GET endpoints to verify configuration retrieval
   - Test all PUT endpoints with valid and invalid data

2. **Simulation Testing**:
   - Configure simulation parameters via API
   - Trigger scenario E via API
   - Monitor metrics at `/metrics`
   - Verify state transitions
   - Test stop functionality

3. **Integration Testing**:
   - Test configuration changes during simulation
   - Verify simulation respects updated configuration
   - Test panel button simulation endpoints

## Architecture Notes

### Why No Separate Application Layer?
Following the existing codebase pattern, configuration use cases are implemented directly in Host layer controllers. This avoids creating an unnecessary abstraction layer when the Host already provides the API boundary.

### Interface Location
`ISimulationScenarioRunner` is in the Simulation project to avoid circular dependencies. The Host project references Simulation, not the other way around.

### Configuration Storage
- System configuration: LiteDB (hot-reloadable)
- Topology configuration: JSON file (requires restart) or default in-memory
- Simulation configuration: Runtime memory (via SimulationConfigController)

## Future Enhancements

1. **Topology Update API**: Implement file-based topology updates
2. **Panel Button Automation**: Automatic simulation trigger when IsSimulationEnabled=true
3. **Multiple Scenarios**: Support for scenarios A, B, C, D, etc.
4. **Configuration Validation**: More comprehensive cross-configuration validation
5. **Configuration History**: Track configuration changes over time
