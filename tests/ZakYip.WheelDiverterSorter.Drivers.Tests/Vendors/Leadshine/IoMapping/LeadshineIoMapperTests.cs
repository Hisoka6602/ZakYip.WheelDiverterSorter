using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.IoBinding;
using ZakYip.WheelDiverterSorter.Core.Topology;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine.IoMapping;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Vendors.Leadshine.IoMapping;

public class LeadshineIoMapperTests
{
    private readonly Mock<ILogger<LeadshineIoMapper>> _loggerMock;

    public LeadshineIoMapperTests()
    {
        _loggerMock = new Mock<ILogger<LeadshineIoMapper>>();
    }

    [Fact]
    public void VendorId_ShouldReturnLeadshine()
    {
        // Arrange
        var config = new LeadshineIoMappingConfig();
        var mapper = new LeadshineIoMapper(_loggerMock.Object, config);

        // Act
        var vendorId = mapper.VendorId;

        // Assert
        Assert.Equal("Leadshine", vendorId);
    }

    [Fact]
    public void MapIoPoint_WithConfiguredMapping_ShouldReturnVendorAddress()
    {
        // Arrange
        var config = new LeadshineIoMappingConfig
        {
            PointMappings = new Dictionary<string, LeadshinePointMapping>
            {
                ["EntrySensor"] = new() { CardNumber = 0, BitNumber = 5 },
                ["D1_Left"] = new() { CardNumber = 0, BitNumber = 10 }
            }
        };
        var mapper = new LeadshineIoMapper(_loggerMock.Object, config);
        var ioPoint = new IoPointDescriptor
        {
            LogicalName = "EntrySensor",
            IoType = IoPointType.DigitalInput
        };

        // Act
        var vendorAddress = mapper.MapIoPoint(ioPoint);

        // Assert
        Assert.NotNull(vendorAddress);
        Assert.Equal("EntrySensor", vendorAddress.LogicalName);
        Assert.Equal("Card0_Bit5", vendorAddress.VendorAddress);
        Assert.Equal(0, vendorAddress.CardNumber);
        Assert.Equal(5, vendorAddress.BitNumber);
    }

    [Fact]
    public void MapIoPoint_WithoutMapping_ShouldReturnNull()
    {
        // Arrange
        var config = new LeadshineIoMappingConfig();
        var mapper = new LeadshineIoMapper(_loggerMock.Object, config);
        var ioPoint = new IoPointDescriptor
        {
            LogicalName = "UnmappedPoint",
            IoType = IoPointType.DigitalInput
        };

        // Act
        var vendorAddress = mapper.MapIoPoint(ioPoint);

        // Assert
        Assert.Null(vendorAddress);
    }

    [Fact]
    public void MapIoPoints_ShouldMapMultiplePoints()
    {
        // Arrange
        var config = new LeadshineIoMappingConfig
        {
            PointMappings = new Dictionary<string, LeadshinePointMapping>
            {
                ["EntrySensor"] = new() { CardNumber = 0, BitNumber = 5 },
                ["D1_Left"] = new() { CardNumber = 0, BitNumber = 10 },
                ["D1_Right"] = new() { CardNumber = 0, BitNumber = 12 }
            }
        };
        IVendorIoMapper mapper = new LeadshineIoMapper(_loggerMock.Object, config);
        var ioPoints = new List<IoPointDescriptor>
        {
            new() { LogicalName = "EntrySensor", IoType = IoPointType.DigitalInput },
            new() { LogicalName = "D1_Left", IoType = IoPointType.DigitalOutput },
            new() { LogicalName = "D1_Right", IoType = IoPointType.DigitalOutput }
        };

        // Act
        var vendorAddresses = mapper.MapIoPoints(ioPoints);

        // Assert
        Assert.Equal(3, vendorAddresses.Count);
        Assert.Contains(vendorAddresses, a => a.LogicalName == "EntrySensor");
        Assert.Contains(vendorAddresses, a => a.LogicalName == "D1_Left");
        Assert.Contains(vendorAddresses, a => a.LogicalName == "D1_Right");
    }

    [Fact]
    public void ValidateProfile_WithAllMappedPoints_ShouldReturnValid()
    {
        // Arrange
        var config = new LeadshineIoMappingConfig
        {
            PointMappings = new Dictionary<string, LeadshinePointMapping>
            {
                ["EntrySensor"] = new() { CardNumber = 0, BitNumber = 5 },
                ["D1_Left"] = new() { CardNumber = 0, BitNumber = 10 }
            }
        };
        var mapper = new LeadshineIoMapper(_loggerMock.Object, config);
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
            }
        };

        // Act
        var (isValid, errorMessage) = mapper.ValidateProfile(profile);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ValidateProfile_WithUnmappedPoints_ShouldReturnInvalid()
    {
        // Arrange
        var config = new LeadshineIoMappingConfig
        {
            PointMappings = new Dictionary<string, LeadshinePointMapping>
            {
                ["EntrySensor"] = new() { CardNumber = 0, BitNumber = 5 }
            }
        };
        var mapper = new LeadshineIoMapper(_loggerMock.Object, config);
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
                        LogicalName = "UnmappedActuator",
                        IoType = IoPointType.DigitalOutput
                    },
                    ActuatorType = ActuatorBindingType.DiverterLeft
                }
            }
        };

        // Act
        var (isValid, errorMessage) = mapper.ValidateProfile(profile);

        // Assert
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("UnmappedActuator", errorMessage);
    }
}
