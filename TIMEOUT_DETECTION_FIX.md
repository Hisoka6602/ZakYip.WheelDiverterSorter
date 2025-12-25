# Timeout Detection Configuration Fix

## Issue Summary

**Problem**: Timeout detection was still triggering even after user disabled it via `/api/sorting/detection-switches` before creating parcels.

**Log Evidence**:
```
2025-12-26 01:09:41.2542|WARN|SortingOrchestrator|包裹 1766682568809 在 Position 2 超时 (延迟 752.3515ms)，使用回退动作 Straight
```

User reported: "我已经在/api/sorting/detection-switches里面关闭了超时，为什么执行分拣开始检查了超时并做了回退？我在程序运行时就已经关闭了超时，那时还未开始创建包裹"

## Root Cause

The system was using a **task-level flag** (`EnableTimeoutDetection`) that was **captured at task creation time** and stored in the queue task. This meant that:

1. When tasks were created, they captured the current value of `ConveyorSegmentConfiguration.EnableLossDetection`
2. This captured value was stored in the `PositionQueueItem.EnableTimeoutDetection` field
3. When the sensor triggered later (Position 2 in this case), the system checked the **stored task-level flag** instead of reading the **current database configuration**

**Timeline**:
- T0: Tasks created with `EnableTimeoutDetection = true` (old configuration)
- T1: User calls `/api/sorting/detection-switches` → updates database to `EnableLossDetection = false`
- T2: Sensor triggers at Position 2 → checks task's stored `EnableTimeoutDetection = true` → **timeout still triggers**

## Solution

Modified `SortingOrchestrator.OnFrontSensorTriggered()` to read the **current real-time configuration** from the database instead of using the task-level captured flag:

```csharp
// Before (Line 1405)
var isTimeout = task.EnableTimeoutDetection && 
               currentTime > task.ExpectedArrivalTime.AddMilliseconds(task.TimeoutThresholdMs);

// After (Lines 1401-1423)
bool enableTimeoutDetection = task.EnableTimeoutDetection; // fallback
        
if (_topologyRepository != null && _segmentRepository != null)
{
    var topology = _topologyRepository.Get();
    var node = topology?.DiverterNodes.FirstOrDefault(n => n.PositionIndex == positionIndex);
    if (node != null)
    {
        var segment = _segmentRepository.GetById(node.SegmentId);
        if (segment != null)
        {
            // Use current real-time configuration
            enableTimeoutDetection = segment.EnableLossDetection;
        }
    }
}

var isTimeout = enableTimeoutDetection && 
               currentTime > task.ExpectedArrivalTime.AddMilliseconds(task.TimeoutThresholdMs);
```

## How It Works

1. When a sensor triggers at a position, the system looks up the topology configuration to find which segment corresponds to that position
2. It then reads the **current** `EnableLossDetection` value from the segment configuration in the database
3. This ensures that configuration changes via `/api/sorting/detection-switches` take effect **immediately**, even for tasks already in the queue

## Testing

### Manual Test Scenario

1. Start the system
2. Create some parcels (tasks will be enqueued)
3. Call `PUT /api/sorting/detection-switches` with `{ "enableTimeoutDetection": false }`
4. Trigger sensors → **No timeout warnings should appear** even if parcels are delayed

### Expected Behavior

- **Before fix**: Timeout warnings would still appear because tasks captured old configuration
- **After fix**: No timeout warnings because system reads current configuration from database

## Files Modified

- `src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs` (lines 1401-1423)

## Related Documentation

- `/api/sorting/detection-switches` API endpoint (SortingController.cs, lines 835-1063)
- `ConveyorSegmentConfiguration.EnableLossDetection` field
- `PositionQueueItem.EnableTimeoutDetection` field (task-level flag, now used only as fallback)

## Notes

- The task-level `EnableTimeoutDetection` flag is still used as a fallback in case repositories are not available
- This ensures defensive programming - if the database is unavailable, the system falls back to the captured value
- The fix is minimal and focused - only changes the timeout detection logic without affecting task creation or other mechanisms
