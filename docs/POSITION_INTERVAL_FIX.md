# Position Interval Tracking Fix

## 修复历史

### Fix 1 (2025-12-14): Missing Position 1 Data
**问题**: Position 1 (第一个摆轮) 的间隔数据缺失  
**原因**: 未记录 position 0 (入口) 的时间戳  
**解决方案**: 在包裹创建时记录 position 0 时间戳

### Fix 2 (2025-12-27): Growing Intervals Over Time
**问题**: Position 间隔随时间推移越来越大  
**原因**: 自动清理误删除仍在运输中的包裹位置记录  
**解决方案**: 移除自动清理，仅在包裹完成分拣时显式清理

---

## Fix 2: Growing Intervals (2025-12-27)

### Problem Summary

Position interval tracking showed growing intervals over time in production logs:
- Normal intervals: ~5700-5800ms
- Spike intervals: 10306ms, 14093ms, 10359ms, etc.
- Pattern: Intervals increased as system processed more parcels

### Root Cause

The `CleanupOldParcelRecords()` method removed parcel position records based on:
1. **Cleanup trigger**: When `_parcelPositionTimes.Count > 1000`
2. **Deletion strategy**: Remove oldest parcels by ParcelId (timestamp)
3. **Problem**: ParcelId (timestamp) ≠ completion status
4. **Result**: Deleted records of parcels **still in transit**

**Example scenario**:
```
Time 0-100s: 1000 parcels enter system (position 0)
Time 100s: Cleanup triggers, removes parcels 1-200
Time 105s: Parcel 150 arrives at position 1
         ❌ Position 0 record missing → Interval calculation fails
```

### Solution

**Removed automatic cleanup** in `RecordParcelPosition`:
- ❌ Before: Auto-cleanup based on count threshold
- ✅ After: Only explicit cleanup via `ClearParcelTracking()`

**Cleanup now happens only when**:
- Parcel completes sorting (`RecordSortingResultAsync` line 1120)
- Parcel is detected as lost (`ParcelLossMonitoringService`)

### Code Changes

1. **PositionIntervalTracker.cs**:
   - Removed lines 148-152: Auto-cleanup trigger
   - Removed lines 270-301: `CleanupOldParcelRecords()` method
   - Removed config: `ParcelRecordCleanupThreshold`, `ParcelRecordRetentionCount`

2. **WheelDiverterSorterServiceCollectionExtensions.cs**:
   - Removed obsolete config references

3. **Tests**:
   - Added regression test: `RecordParcelPosition_WithManyParcels_ShouldNotAutoCleanupInTransitParcels`

### Verification

**Test Results**: All 8 tests passing ✅

**Expected behavior after fix**:
- ✅ Intervals remain stable over time
- ✅ No spike intervals from missing position records
- ✅ Memory grows but bounded by parcel completion rate

---

## Fix 1: Missing Position 1 Data (2025-12-14)

## Before Fix (Broken)

```
Entry Sensor → Diverter 1 → Diverter 2
   (no pos)     (pos 1)      (pos 2)
                    ↓            ↓
                 ❌ No data   ✅ Has data
                 (looking     (found pos 1)
                  for pos 0)
```

**API Response with 2 diverters:**
```json
{
  "intervals": [
    {
      "positionIndex": 2,
      "medianIntervalMs": 5692.67,
      "sampleCount": 10
    }
  ]
}
```
❌ Missing position 1 data!

## After Fix (Working)

```
Entry Sensor → Diverter 1 → Diverter 2
   (pos 0)      (pos 1)      (pos 2)
      ↓            ↓            ↓
   Record      ✅ Has data   ✅ Has data
   entry       (found pos 0) (found pos 1)
   time
```

**API Response with 2 diverters:**
```json
{
  "intervals": [
    {
      "positionIndex": 1,
      "medianIntervalMs": 5692.67,
      "sampleCount": 10,
      "minIntervalMs": 5633.97,
      "maxIntervalMs": 6058.81
    },
    {
      "positionIndex": 2,
      "medianIntervalMs": 5800.45,
      "sampleCount": 10,
      "minIntervalMs": 5700.12,
      "maxIntervalMs": 6100.23
    }
  ]
}
```
✅ All positions have data!

## Technical Details

### Code Changes

1. **Record entry position (position 0) when parcel is created:**
   ```csharp
   // In SortingOrchestrator.CreateParcelEntityAsync()
   _intervalTracker?.RecordParcelPosition(parcelId, 0, _clock.LocalNow);
   ```

2. **Enable interval calculation from position 0:**
   ```csharp
   // In PositionIntervalTracker.RecordParcelPosition()
   // Changed from: if (positionIndex > 1)
   // Changed to:   if (positionIndex > 0)
   ```

3. **Include position 1 in statistics:**
   ```csharp
   // In PositionIntervalTracker.GetAllStatistics()
   // Changed from: .Where(k => k > 1)
   // Changed to:   .Where(k => k > 0)
   ```

### Position Index Mapping

| Location | Position Index | Interval Calculated From |
|----------|----------------|-------------------------|
| Entry Sensor (ParcelCreation) | 0 | N/A (no previous position) |
| Diverter 1 Front Sensor | 1 | Position 0 (entry) |
| Diverter 2 Front Sensor | 2 | Position 1 |
| Diverter 3 Front Sensor | 3 | Position 2 |
| Diverter N Front Sensor | N | Position N-1 |

### Interval Calculation Logic

For each parcel passing through the system:

1. **Entry sensor triggered:**
   - Record: `parcelPositionTimes[parcelId][0] = entryTime`
   - No interval calculation (first position)

2. **Diverter 1 front sensor triggered:**
   - Record: `parcelPositionTimes[parcelId][1] = position1Time`
   - Calculate: `interval = position1Time - parcelPositionTimes[parcelId][0]`
   - Store: `intervalHistory[1].Add(interval)`

3. **Diverter 2 front sensor triggered:**
   - Record: `parcelPositionTimes[parcelId][2] = position2Time`
   - Calculate: `interval = position2Time - parcelPositionTimes[parcelId][1]`
   - Store: `intervalHistory[2].Add(interval)`

4. **Diverter N front sensor triggered:**
   - Record: `parcelPositionTimes[parcelId][N] = positionNTime`
   - Calculate: `interval = positionNTime - parcelPositionTimes[parcelId][N-1]`
   - Store: `intervalHistory[N].Add(interval)`

## Testing

All test cases passing (6/6):

1. ✅ Entry to Position 1 interval recording
2. ✅ Position 1 to Position 2 interval recording
3. ✅ Multiple parcels with all position intervals
4. ✅ GetAllStatistics includes position 1 data
5. ✅ Position 0 has no interval data (internal use only)
6. ✅ Skips interval calculation when previous position missing

## Related Files

- `src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs`
- `src/Execution/ZakYip.WheelDiverterSorter.Execution/Tracking/PositionIntervalTracker.cs`
- `tests/ZakYip.WheelDiverterSorter.Execution.Tests/Tracking/PositionIntervalTrackerTests.cs`

## API Endpoint

**GET** `/api/sorting/position-intervals`

Returns interval statistics for all positions (now including position 1).

---

**Date:** 2025-12-14  
**Issue:** Missing position interval data for first diverter  
**Status:** ✅ Fixed and tested
