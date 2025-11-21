# Path Failure Detection and Monitoring Guide

## Overview

This system provides **monitoring and event notification** for path execution failures in the wheel diverter sorter. When a path segment or full path execution fails, the system **logs the failure, raises events, and calculates a backup path** for reference purposes. 

**Important Note**: The system does **NOT** automatically redirect or re-execute failed parcels. When a path execution fails, the parcel has already been physically routed according to the original path execution result. The backup path calculation is primarily for logging and monitoring purposes.

## Features

### 1. Path Segment Execution Failure Detection (路径段执行失败检测)

The system monitors each segment of the path execution and detects failures at the segment level:

```csharp
// Event raised when a segment fails
public event EventHandler<PathSegmentExecutionFailedEventArgs>? SegmentExecutionFailed;

// Event includes:
// - ParcelId: Package identifier
// - FailedSegment: The specific segment that failed
// - OriginalTargetChuteId: Original destination
// - FailureReason: Detailed reason for failure
// - FailureTime: Timestamp of failure
// - FailurePosition: DiverterId where failure occurred
```

### 2. Failure Reason and Position Recording (记录失败原因和位置)

All failures are recorded with complete context:

```csharp
public record class PathExecutionResult
{
    public required bool IsSuccess { get; init; }
    public required int ActualChuteId { get; init; }
    public string? FailureReason { get; init; }
    public SwitchingPathSegment? FailedSegment { get; init; }  // Which segment failed
    public DateTimeOffset? FailureTime { get; init; }           // When it failed
}
```

### 3. Backup Path Calculation (备用路径计算-异常格口，仅用于记录)

When a failure is detected, the system calculates a backup path to the exception chute **for logging and monitoring purposes only**. This backup path is **NOT automatically executed**.

```csharp
// Backup path calculation (for reference only, not executed)
public interface IPathFailureHandler
{
    SwitchingPath? CalculateBackupPath(SwitchingPath originalPath);
}

// Uses the FallbackChuteId from the original path
var backupPath = _pathFailureHandler.CalculateBackupPath(originalPath);
// Note: This path is only logged, not executed
```

**Clarification**: The parcel has already been physically routed based on the execution result of the original path. The backup path calculation serves as a reference for what path *should* have been taken to route to the exception chute, but it cannot retroactively change the physical parcel routing.

### 4. Path Failure Notification (路径失败通知，非自动切换)

**Important**: The system does **NOT** automatically switch or redirect parcels when a path execution fails. The term "automatic" refers only to automatic **event notification and logging**, not physical parcel redirection.

What actually happens in `ParcelSortingOrchestrator`:

```csharp
var executionResult = await _pathExecutor.ExecuteAsync(path);

if (!executionResult.IsSuccess)
{
    // Handle failure: log and raise events (NO physical redirection occurs)
    if (_pathFailureHandler != null)
    {
        _pathFailureHandler.HandlePathFailure(
            parcelId,
            path,
            executionResult.FailureReason ?? "未知错误",
            executionResult.FailedSegment);
    }
    // Note: The parcel has already been physically routed based on the
    // execution result. This handler only logs the failure and raises events
    // for monitoring purposes. It does NOT redirect the parcel.
}
```

**Key Point**: By the time path execution fails, the physical parcel has already moved through the diverters according to the executed path (successful or failed segments). The failure handler cannot retroactively change the physical location of the parcel.

### 5. Event Notifications (事件通知，用于监控)

Three types of events are available for monitoring (these are notifications only, not commands to redirect parcels):

```csharp
// 1. Segment-level failure
_handler.SegmentExecutionFailed += (sender, args) => 
{
    Console.WriteLine($"Segment {args.FailedSegment.SequenceNumber} failed at diverter {args.FailurePosition}");
};

// 2. Path-level failure
_handler.PathExecutionFailed += (sender, args) => 
{
    Console.WriteLine($"Path execution failed: {args.FailureReason}");
};

// 3. Path switching
_handler.PathSwitched += (sender, args) => 
{
    Console.WriteLine($"Switched from chute {args.OriginalPath.TargetChuteId} to {args.BackupPath.TargetChuteId}");
};
```

### 6. Comprehensive Logging (记录路径切换日志)

All failure events are automatically logged:

```
[Warning] 路径段执行失败: ParcelId=12345, 段序号=1, 摆轮=5, 原因=执行超时
[Error] 路径执行失败: ParcelId=12345, 原始目标格口=101, 失败原因=段1执行超时, 将切换到异常格口=999
[Information] 已计算备用路径: ParcelId=12345, 目标格口=999, 路径段数=2
```

## Usage Example

### Basic Integration

```csharp
// 1. Create path generator
var pathGenerator = new DefaultSwitchingPathGenerator(routeRepository);

// 2. Create failure handler
var failureHandler = new PathFailureHandler(
    pathGenerator,
    logger);

// 3. Subscribe to events (optional)
failureHandler.PathExecutionFailed += (sender, args) => 
{
    // Handle failure event
    Console.WriteLine($"Package {args.ParcelId} failed, switched to chute {args.ActualChuteId}");
};

// 4. Integrate with orchestrator
var orchestrator = new ParcelSortingOrchestrator(
    parcelDetectionService,
    ruleEngineClient,
    pathGenerator,
    pathExecutor,
    options,
    systemConfigRepository,
    logger,
    failureHandler);  // Pass the failure handler
```

### Manual Failure Handling

```csharp
// Execute path
var result = await executor.ExecuteAsync(path);

// Check for failure
if (!result.IsSuccess)
{
    // Handle the failure (logs and raises events)
    failureHandler.HandlePathFailure(
        parcelId: 12345,
        originalPath: path,
        failureReason: result.FailureReason ?? "Unknown error",
        failedSegment: result.FailedSegment);
    
    // Calculate backup path for reference/logging only
    var backupPath = failureHandler.CalculateBackupPath(path);
    if (backupPath != null)
    {
        // Note: In the actual implementation, the backup path is NOT executed
        // because the parcel has already been physically routed.
        // You would only execute the backup path if you're implementing
        // a NEW physical sorting flow for a NEW parcel, not for recovering
        // an already-failed parcel.
        
        // await executor.ExecuteAsync(backupPath); // NOT done in practice
    }
}
```

## Configuration

The failure handler uses the `FallbackChuteId` property from `SwitchingPath`:

```csharp
var path = new SwitchingPath
{
    TargetChuteId = 101,
    FallbackChuteId = 999,  // Exception chute for failures
    Segments = segments,
    GeneratedAt = DateTimeOffset.UtcNow
};
```

## Testing

### Unit Tests

```bash
# Run all path failure tests
dotnet test --filter "FullyQualifiedName~PathFailure"

# Results: 13/13 tests passing
# - 10 unit tests (PathFailureHandlerTests)
# - 3 integration tests (PathFailureIntegrationTests)
```

### Example Test

```csharp
[Fact]
public async Task PathExecution_WhenFails_HandlerReceivesFailureEvent()
{
    // Arrange
    var handler = new PathFailureHandler(pathGenerator, logger);
    PathExecutionFailedEventArgs? capturedEvent = null;
    handler.PathExecutionFailed += (sender, args) => capturedEvent = args;
    
    // Act
    var result = await failingExecutor.ExecuteAsync(path);
    handler.HandlePathFailure(parcelId, path, result.FailureReason, result.FailedSegment);
    
    // Assert
    Assert.NotNull(capturedEvent);
    Assert.Equal(parcelId, capturedEvent.ParcelId);
}
```

## Troubleshooting

### No Backup Path Generated

If `CalculateBackupPath` returns `null`, check:
1. The `FallbackChuteId` is valid
2. A route configuration exists for the fallback chute
3. The route configuration has valid diverter entries

### Events Not Firing

Ensure the failure handler is properly instantiated and subscribed to before path execution.

### Failures Not Logged

Check that logging is properly configured and the log level includes `Warning` and `Error` levels.

## Architecture

```
ParcelSortingOrchestrator
    ├─ ISwitchingPathExecutor (executes path)
    │   └─ Returns PathExecutionResult with failure details
    │
    └─ IPathFailureHandler (handles failures)
        ├─ Raises Events:
        │   ├─ SegmentExecutionFailed
        │   ├─ PathExecutionFailed  
        │   └─ PathSwitched
        │
        └─ Calculates backup path to exception chute
```

## Performance Considerations

- Event handling is synchronous; avoid long-running operations in event handlers
- Backup path calculation reuses the existing path generator (cached)
- No additional database queries for failure handling
- Minimal memory overhead (event args are small objects)

## System Limitations and Design Constraints

### Current Behavior
The current system provides **monitoring and notification** of path execution failures, but does **NOT** provide automatic physical parcel redirection because:

1. **Physical Reality**: Once a diverter has been activated and the parcel has moved, it cannot be retroactively redirected
2. **Linear Topology**: Parcels move forward only through the system; there is no mechanism to reverse direction
3. **Real-time Execution**: Path execution happens in real-time as the parcel physically moves through diverters

### What the System Does
- ✅ Detects when path segments fail during execution
- ✅ Records failure context (which segment, reason, timing)
- ✅ Calculates what the backup path should be (for analysis/logging)
- ✅ Raises events for monitoring systems
- ✅ Logs detailed failure information

### What the System Does NOT Do
- ❌ Physically redirect an already-failed parcel to a different chute
- ❌ Automatically execute backup paths for failed parcels
- ❌ Reverse or retry physical diverter actions after failure

### Exception Handling Strategy
When a path execution fails:
1. The parcel physically ends up where the failed execution left it
2. The system logs this as a failure
3. For FUTURE parcels with the same target, the system could potentially:
   - Route them through alternative paths (if multiple paths exist)
   - Route them to exception chute if the original path is unreliable
   - But this is NOT automatic redirection of already-failed parcels

## Future Enhancements

Potential improvements for future versions:

1. **Retry Logic**: For transient failures (before parcel has moved), retry the diverter action
2. **Failure Statistics**: Track failure rates per diverter/segment
3. **Predictive Maintenance**: Alert when failure rates exceed thresholds  
4. **Alternative Path Selection**: When failures occur, automatically route FUTURE parcels through alternative paths
5. **Multi-Path Support**: Implement topology-based routing with multiple paths to same destination
6. **Failure Recovery**: For certain failure types, attempt corrective actions before the parcel has passed

## Related Documentation

- [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)
- [API_USAGE_GUIDE.md](API_USAGE_GUIDE.md)
- [TESTING.md](TESTING.md)
