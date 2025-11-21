# Driver and Sensor IO Separation Architecture

## Overview
Per requirement #3, driver control and IO trigger sensing are architecturally separated as they likely won't be from the same vendor in production environments.

## Current Architecture

### 1. Clear Abstraction Boundaries

**Driver Control (Output)**:
- Interface: `IDiverterController`
- Location: `ZakYip.WheelDiverterSorter.Drivers.Abstractions`
- Purpose: Controls physical wheel diverter hardware
- Operations: SetAngleAsync, GetCurrentAngleAsync, ResetAsync
- Example Implementation: `LeadshineDiverterController` (uses LTDMC output operations)

**Sensor IO (Input)**:
- Interface: `ISensor`
- Location: `ZakYip.WheelDiverterSorter.Ingress`
- Purpose: Detects parcel presence through IO triggers
- Operations: StartAsync, StopAsync, Events: SensorTriggered, SensorError
- Example Implementation: `LeadshineLaserSensor`, `LeadshinePhotoelectricSensor` (uses `IInputPort` abstraction)

### 2. Separate Configuration

**Driver Configuration**:
```
DriverOptions
  ├─ UseHardwareDriver: bool
  └─ Leadshine: LeadshineOptions
       ├─ CardNo: ushort
       └─ Diverters: List<LeadshineDiverterConfigDto>
            ├─ DiverterId: int
            ├─ DiverterName: string
            ├─ ConnectedConveyorLengthMm: double
            ├─ ConnectedConveyorSpeedMmPerSec: double
            ├─ DiverterSpeedMmPerSec: double
            ├─ OutputStartBit: int
            └─ FeedbackInputBit: int?
```

**Sensor Configuration**:
```
SensorOptions
  ├─ UseHardwareSensor: bool
  ├─ VendorType: string
  ├─ Leadshine: LeadshineSensorOptions
  │    ├─ CardNo: ushort
  │    └─ Sensors: List<LeadshineSensorConfigDto>
  └─ MockSensors: List<MockSensorConfigDto>
```

### 3. Independent Registration

Drivers and sensors are registered independently in dependency injection:

**Driver Registration**: `DriverServiceExtensions.AddDrivers()`
- Registers `IDiverterController` implementations
- Uses driver-specific configuration

**Sensor Registration**: `SensorServiceExtensions.AddSensors()`
- Registers `ISensor` implementations
- Uses sensor-specific configuration

### 4. Vendor Independence

While Leadshine is used as an example implementation for both:
- Both use the same hardware library (LTDMC) but through **different abstractions**
- Drivers use `LTDMC.dmc_write_outbit()` directly
- Sensors use `IInputPort` abstraction → `LeadshineInputPort` → `LTDMC.dmc_read_inbit()`
- Each can easily be replaced with different vendor implementations
- No shared state or coupling between driver and sensor implementations

### 5. Benefits of Current Separation

1. **Different Vendors**: Can use Vendor A for drivers and Vendor B for sensors
2. **Different Hardware**: Driver can use Controller Card #1, Sensors can use Card #2
3. **Independent Lifecycle**: Drivers and sensors start/stop independently
4. **Separate Error Handling**: Failures in one don't affect the other
5. **Clear Responsibilities**: Each component has a single, well-defined purpose

## Example: Using Different Vendors

To use different vendors, implement the interfaces:

```csharp
// For sensors - implement ISensor or IInputPort
public class VendorBSensorInputPort : IInputPort { ... }

// For drivers - implement IDiverterController or IOutputPort  
public class VendorADiverterController : IDiverterController { ... }
```

Then register them independently in the service collection.

## Conclusion

The driver and sensor IO operations are **already properly separated** at the architectural level through:
- Distinct interfaces and abstractions
- Separate configuration structures
- Independent registration and lifecycle management
- No shared implementation code

This separation allows production systems to use different hardware vendors for driver control and sensor IO without any code changes to the core system.
