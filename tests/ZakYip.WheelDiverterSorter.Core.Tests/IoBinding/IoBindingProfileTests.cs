using Xunit;
using ZakYip.WheelDiverterSorter.Core.IoBinding;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.Tests.IoBinding;

public class IoBindingProfileTests
{
    [Fact]
    public void IoBindingProfile_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var profile = new IoBindingProfile
        {
            ProfileId = "PROFILE_001",
            ProfileName = "Test Profile",
            TopologyId = "TOPO_001",
            Description = "Test IO binding profile",
            SensorBindings = new List<SensorBinding>
            {
                new()
                {
                    IoPoint = new IoPointDescriptor
                    {
                        LogicalName = "EntrySensor",
                        IoType = IoPointType.DigitalInput
                    },
                    SensorType = SensorBindingType.Entry
                }
            },
            ActuatorBindings = new List<ActuatorBinding>
            {
                new()
                {
                    IoPoint = new IoPointDescriptor
                    {
                        LogicalName = "D1_Left",
                        IoType = IoPointType.DigitalOutput
                    },
                    ActuatorType = ActuatorBindingType.DiverterLeft,
                    NodeId = "D1"
                }
            }
        };

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("PROFILE_001", profile.ProfileId);
        Assert.Equal("Test Profile", profile.ProfileName);
        Assert.Equal("TOPO_001", profile.TopologyId);
        Assert.Single(profile.SensorBindings);
        Assert.Single(profile.ActuatorBindings);
    }

    [Fact]
    public void FindSensorBinding_WithExistingName_ShouldReturnBinding()
    {
        // Arrange
        var profile = new IoBindingProfile
        {
            ProfileId = "PROFILE_001",
            ProfileName = "Test Profile",
            TopologyId = "TOPO_001",
            SensorBindings = new List<SensorBinding>
            {
                new()
                {
                    IoPoint = new IoPointDescriptor
                    {
                        LogicalName = "EntrySensor",
                        IoType = IoPointType.DigitalInput
                    },
                    SensorType = SensorBindingType.Entry
                },
                new()
                {
                    IoPoint = new IoPointDescriptor
                    {
                        LogicalName = "D1_Sensor",
                        IoType = IoPointType.DigitalInput
                    },
                    SensorType = SensorBindingType.Node,
                    NodeId = "D1"
                }
            },
            ActuatorBindings = Array.Empty<ActuatorBinding>()
        };

        // Act
        var binding = profile.FindSensorBinding("D1_Sensor");

        // Assert
        Assert.NotNull(binding);
        Assert.Equal("D1_Sensor", binding.IoPoint.LogicalName);
        Assert.Equal(SensorBindingType.Node, binding.SensorType);
        Assert.Equal("D1", binding.NodeId);
    }

    [Fact]
    public void FindActuatorBinding_WithExistingName_ShouldReturnBinding()
    {
        // Arrange
        var profile = new IoBindingProfile
        {
            ProfileId = "PROFILE_001",
            ProfileName = "Test Profile",
            TopologyId = "TOPO_001",
            SensorBindings = Array.Empty<SensorBinding>(),
            ActuatorBindings = new List<ActuatorBinding>
            {
                new()
                {
                    IoPoint = new IoPointDescriptor
                    {
                        LogicalName = "D1_Left",
                        IoType = IoPointType.DigitalOutput
                    },
                    ActuatorType = ActuatorBindingType.DiverterLeft,
                    NodeId = "D1"
                }
            }
        };

        // Act
        var binding = profile.FindActuatorBinding("D1_Left");

        // Assert
        Assert.NotNull(binding);
        Assert.Equal("D1_Left", binding.IoPoint.LogicalName);
        Assert.Equal(ActuatorBindingType.DiverterLeft, binding.ActuatorType);
    }

    [Fact]
    public void GetAllIoPoints_ShouldReturnAllPoints()
    {
        // Arrange
        var profile = new IoBindingProfile
        {
            ProfileId = "PROFILE_001",
            ProfileName = "Test Profile",
            TopologyId = "TOPO_001",
            SensorBindings = new List<SensorBinding>
            {
                new()
                {
                    IoPoint = new IoPointDescriptor
                    {
                        LogicalName = "EntrySensor",
                        IoType = IoPointType.DigitalInput
                    },
                    SensorType = SensorBindingType.Entry
                }
            },
            ActuatorBindings = new List<ActuatorBinding>
            {
                new()
                {
                    IoPoint = new IoPointDescriptor
                    {
                        LogicalName = "D1_Left",
                        IoType = IoPointType.DigitalOutput
                    },
                    ActuatorType = ActuatorBindingType.DiverterLeft
                }
            },
            OtherIoPoints = new List<IoPointDescriptor>
            {
                new()
                {
                    LogicalName = "StatusLED",
                    IoType = IoPointType.DigitalOutput
                }
            }
        };

        // Act
        var allPoints = profile.GetAllIoPoints().ToList();

        // Assert
        Assert.Equal(3, allPoints.Count);
        Assert.Contains(allPoints, p => p.LogicalName == "EntrySensor");
        Assert.Contains(allPoints, p => p.LogicalName == "D1_Left");
        Assert.Contains(allPoints, p => p.LogicalName == "StatusLED");
    }

    [Fact]
    public void FindSensorBinding_WithNonExistentName_ShouldReturnNull()
    {
        // Arrange
        var profile = new IoBindingProfile
        {
            ProfileId = "PROFILE_001",
            ProfileName = "Test Profile",
            TopologyId = "TOPO_001",
            SensorBindings = Array.Empty<SensorBinding>(),
            ActuatorBindings = Array.Empty<ActuatorBinding>()
        };

        // Act
        var binding = profile.FindSensorBinding("NonExistent");

        // Assert
        Assert.Null(binding);
    }
}
