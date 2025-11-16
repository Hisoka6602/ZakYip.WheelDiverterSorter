# Upstream Chute Change (改口) Feature Implementation Summary

## Overview
This implementation adds the upstream chute change (改口) feature to the wheel diverter sorter system, allowing upstream systems to request target chute changes for parcels that are still being sorted.

## Core Business Rules

The implementation strictly follows the business principle: **"宁可异常，不得错分"** (Better exception than mis-sort)

1. **Chute changes are ACCEPTED** when:
   - The parcel's route plan is in `Created` or `Executing` status
   - The request is within the replan time window (before LastReplanDeadline if set)

2. **Chute changes are IGNORED** when:
   - The parcel has already completed sorting (`Completed` status)
   - The parcel has already entered the exception routing path (`ExceptionRouted` status)

3. **Chute changes are REJECTED** when:
   - The route plan has failed (`Failed` status)
   - The request arrives after the replan deadline (`RejectedTooLate`)

## Architecture

### 1. Core Layer (Domain Model)
**Location**: `ZakYip.WheelDiverterSorter.Core`

**New Components**:
- `RoutePlanStatus` enum - Lifecycle states (Created, Executing, Completed, ExceptionRouted, Failed)
- `ChuteChangeOutcome` enum - Decision results (Accepted, IgnoredAlreadyCompleted, etc.)
- `ChuteChangeDecision` record - Decision result data
- `RoutePlan` aggregate root - Main domain entity with `TryApplyChuteChange()` method
- Event args: `ChuteChangeRequestedEventArgs`, `ChuteChangeAcceptedEventArgs`, `ChuteChangeIgnoredEventArgs`
- `IRoutePlanRepository` interface - Repository abstraction

**Key Method**: `RoutePlan.TryApplyChuteChange()`
```csharp
public OperationResult TryApplyChuteChange(
    int requestedChuteId,
    DateTimeOffset requestedAt,
    out ChuteChangeDecision decision)
```

### 2. Execution Layer (Path Replanning)
**Location**: `ZakYip.WheelDiverterSorter.Execution`

**New Components**:
- `IRouteReplanner` interface - Path replanning abstraction
- `RouteReplanner` class - Implementation that regenerates paths using `ISwitchingPathGenerator`
- `ReplanResult` record - Result data for replan operations

**Key Method**: `IRouteReplanner.ReplanAsync()`
```csharp
Task<ReplanResult> ReplanAsync(
    long parcelId,
    int newTargetChuteId,
    DateTimeOffset replanAt,
    CancellationToken cancellationToken = default)
```

### 3. Application/Host Layer (Command Handling)
**Location**: `ZakYip.WheelDiverterSorter.Host/Commands`

**New Components**:
- `ChangeParcelChuteCommand` - Command object
- `ChangeParcelChuteCommandHandler` - Handler that orchestrates the workflow
- `ChangeParcelChuteResult` - Result object
- `InMemoryRoutePlanRepository` - In-memory repository implementation

**Workflow**:
1. Load route plan from repository
2. Call `TryApplyChuteChange()` on domain entity
3. If accepted, call `ReplanAsync()` to regenerate path
4. Save updated route plan
5. Return result to caller

### 4. API Layer (REST Endpoint)
**Location**: `ZakYip.WheelDiverterSorter.Host/Controllers`

**Endpoint**: `POST /api/diverts/change-chute`

**Request DTO**:
```json
{
  "parcelId": 12345,
  "requestedChuteId": 20,
  "requestedAt": "2025-11-16T22:00:00Z"  // optional
}
```

**Response DTO**:
```json
{
  "isSuccess": true,
  "parcelId": 12345,
  "originalChuteId": 10,
  "requestedChuteId": 20,
  "effectiveChuteId": 20,
  "outcome": "Accepted",
  "message": "Chute change accepted and path replanned successfully",
  "processedAt": "2025-11-16T22:00:01Z"
}
```

### 5. Simulation Layer
**Location**: `ZakYip.WheelDiverterSorter.Simulation`

**Extended Configuration** (`SimulationOptions`):
- `IsEnableUpstreamChuteChange` - Enable/disable chute change simulation
- `UpstreamChuteChangeProbability` - Probability (0.0-1.0) for each parcel
- `MinChuteChangeOffset` - Earliest time for change request
- `MaxChuteChangeOffset` - Latest time for change request
- `ShouldTreatLateChangeAsException` - How to handle late requests

## Testing

### Unit Tests
**Location**: `ZakYip.WheelDiverterSorter.Core.Tests/RoutePlanTests.cs`

11 comprehensive tests covering:
- Initial state creation
- Accepted changes (Created/Executing states)
- Ignored changes (Completed/ExceptionRouted states)
- Rejected changes (Failed state, too late)
- Domain event generation
- Multiple consecutive changes

**Result**: ✅ 11/11 passing

### E2E Tests
**Location**: `ZakYip.WheelDiverterSorter.E2ETests/UpstreamChuteChangeTests.cs`

5 integration tests covering:
- Accepted change scenario with path regeneration
- Ignored change when completed
- Ignored change when exception routed
- Rejected change when too late
- Failure when route plan not found

**Result**: ✅ 5/5 passing

### Security Scan
**Tool**: CodeQL

**Result**: ✅ 0 alerts (No security vulnerabilities found)

## Dependency Injection

All services are registered in `Program.cs`:
```csharp
// Register chute change feature services
builder.Services.AddSingleton<IRoutePlanRepository, InMemoryRoutePlanRepository>();
builder.Services.AddSingleton<IRouteReplanner, RouteReplanner>();
builder.Services.AddSingleton<ChangeParcelChuteCommandHandler>();
```

## API Usage Example

### Using cURL
```bash
curl -X POST http://localhost:5000/api/diverts/change-chute \
  -H "Content-Type: application/json" \
  -d '{
    "parcelId": 12345,
    "requestedChuteId": 20
  }'
```

### Using C#
```csharp
var client = new HttpClient();
var request = new ChuteChangeRequest
{
    ParcelId = 12345,
    RequestedChuteId = 20
};

var response = await client.PostAsJsonAsync(
    "http://localhost:5000/api/diverts/change-chute",
    request);

var result = await response.Content.ReadFromJsonAsync<ChuteChangeResponse>();

Console.WriteLine($"Outcome: {result.Outcome}");
Console.WriteLine($"Effective Chute: {result.EffectiveChuteId}");
```

## Files Changed

### New Files (21 files, 1604+ lines)
- **Core Layer**: 8 files (enums, events, aggregate, repository interface)
- **Execution Layer**: 2 files (replanner interface and implementation)
- **Host Layer**: 7 files (commands, controller, DTOs, repository)
- **Tests**: 2 files (Core unit tests, E2E tests)
- **Configuration**: 1 file (SimulationOptions)
- **Documentation**: 1 file (this summary)

### Modified Files (2 files)
- `Program.cs` - Added DI registrations
- `SimulationOptions.cs` - Extended with chute change fields

## Extension Points

The implementation is designed to be extensible:

1. **Repository**: `IRoutePlanRepository` can be implemented with different backends (database, Redis, etc.)
2. **Replanner**: `IRouteReplanner` can be enhanced with position tracking and advanced timing logic
3. **Simulation**: `SimulationOptions` provides hooks for generating chute change requests in simulation
4. **Events**: Domain events can be published to external systems for monitoring and integration

## Future Enhancements (Not Implemented)

As mentioned in the original requirements but simplified for this implementation:

1. **Simulation Runner Integration**: Automatic chute change request generation during simulation runs
2. **Scenario Tests**: REPLAN-1 and REPLAN-2 simulation scenarios with assertions
3. **Position Tracking**: Real-time parcel position detection to improve replan timing decisions
4. **Event Publishing**: Publish domain events to message bus for external systems
5. **Persistent Storage**: Replace in-memory repository with database-backed implementation

## Conclusion

This implementation provides a solid foundation for the upstream chute change feature with:
- ✅ Clean domain-driven design with clear separation of concerns
- ✅ Comprehensive test coverage (16 tests total)
- ✅ RESTful API endpoint ready for integration
- ✅ Zero security vulnerabilities
- ✅ Extensible architecture for future enhancements

The core business rule **"宁可异常，不得错分"** is enforced at every layer, ensuring the system maintains its integrity even when handling dynamic chute change requests.
