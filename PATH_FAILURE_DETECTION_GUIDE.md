# Path Failure Detection and Recovery Guide

## Overview

This system provides automatic detection and recovery for path execution failures in the wheel diverter sorter. When a path segment or full path execution fails, the system automatically calculates a backup path to the exception chute and handles the failure gracefully.

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

### 3. Backup Path Calculation (备用路径计算-异常格口)

When a failure is detected, the system automatically calculates a backup path to the exception chute:

```csharp
// Automatic backup path calculation
public interface IPathFailureHandler
{
    SwitchingPath? CalculateBackupPath(SwitchingPath originalPath);
}

// Uses the FallbackChuteId from the original path
var backupPath = _pathFailureHandler.CalculateBackupPath(originalPath);
```

### 4. Automatic Path Switching (包裹自动切换路径)

Path switching happens automatically in the `ParcelSortingOrchestrator`:

```csharp
var executionResult = await _pathExecutor.ExecuteAsync(path);

if (!executionResult.IsSuccess)
{
    // Automatically handle failure and calculate backup path
    if (_pathFailureHandler != null)
    {
        _pathFailureHandler.HandlePathFailure(
            parcelId,
            path,
            executionResult.FailureReason ?? "未知错误",
            executionResult.FailedSegment);
    }
}
```

### 5. Event Notifications (通知摆轮执行新路径)

Three types of events are available for monitoring:

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
    // Handle the failure
    failureHandler.HandlePathFailure(
        parcelId: 12345,
        originalPath: path,
        failureReason: result.FailureReason ?? "Unknown error",
        failedSegment: result.FailedSegment);
    
    // Calculate and use backup path if needed
    var backupPath = failureHandler.CalculateBackupPath(path);
    if (backupPath != null)
    {
        await executor.ExecuteAsync(backupPath);
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

## Future Enhancements

Potential improvements for future versions:

1. **Retry Logic**: Automatic retry before switching to backup path
2. **Failure Statistics**: Track failure rates per diverter/segment
3. **Predictive Maintenance**: Alert when failure rates exceed thresholds
4. **Custom Backup Strategies**: Different backup paths based on failure type
5. **Failure Recovery**: Attempt to recover failed segments before switching

## Related Documentation

- [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)
- [API_USAGE_GUIDE.md](API_USAGE_GUIDE.md)
- [TESTING.md](TESTING.md)
