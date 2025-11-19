# PR-04 Test Fixes Required

## HardwareSwitchingPathExecutorTests.cs

This test file needs to be updated to use `IWheelDiverterDriver` instead of `IDiverterController`.

### Changes needed:

1. Replace all `Mock<IDiverterController>` with `Mock<IWheelDiverterDriver>`
2. Replace `SetAngleAsync()` calls with semantic methods:
   - `PassThroughAsync()` for Straight direction
   - `TurnLeftAsync()` for Left direction
   - `TurnRightAsync()` for Right direction
3. Update verification calls to use the new semantic methods
4. Update `Array.Empty<IDiverterController>()` to `Array.Empty<IWheelDiverterDriver>()`

### Helper method already added:

A `CreateMockDriver()` helper method has been added at the top of the test class that creates properly configured mock drivers.

### Tests that need updating:

- All tests that create `IDiverterController` mocks
- Tests with execution order tracking may need special attention

This is straightforward but tedious work - each test method needs the mocks updated to use the new interface.
