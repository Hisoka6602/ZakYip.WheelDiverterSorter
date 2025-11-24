using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.IoBinding;
using ZakYip.WheelDiverterSorter.Core.Enums;
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
        // Arrange
        var config = new LeadshineIoMappingConfig();
        var mapper = new LeadshineIoMapper(_loggerMock.Object, config);
        // Act
        var vendorId = mapper.VendorId;
        // Assert
        Assert.Equal("Leadshine", vendorId);
    public void MapIoPoint_WithConfiguredMapping_ShouldReturnVendorAddress()
        var config = new LeadshineIoMappingConfig
        {
            PointMappings = new Dictionary<string, LeadshinePointMapping>
            {
                ["EntrySensor"] = new() { CardNumber = 0, BitNumber = 5 },
                ["D1_Left"] = new() { CardNumber = 0, BitNumber = 10 }
            }
        };
        var ioPoint = new IoPointDescriptor
            LogicalName = "EntrySensor",
            IoType = IoPointType.DigitalInput
        var vendorAddress = mapper.MapIoPoint(ioPoint);
        Assert.NotNull(vendorAddress);
        Assert.Equal("EntrySensor", vendorAddress.LogicalName);
        Assert.Equal("Card0_Bit5", vendorAddress.VendorAddress);
        Assert.Equal(0, vendorAddress.CardNumber);
        Assert.Equal(5, vendorAddress.BitNumber);
    public void MapIoPoint_WithoutMapping_ShouldReturnNull()
            LogicalName = "UnmappedPoint",
        Assert.Null(vendorAddress);
    public void MapIoPoints_ShouldMapMultiplePoints()
                ["D1_Left"] = new() { CardNumber = 0, BitNumber = 10 },
                ["D1_Right"] = new() { CardNumber = 0, BitNumber = 12 }
        IVendorIoMapper mapper = new LeadshineIoMapper(_loggerMock.Object, config);
        var ioPoints = new List<IoPointDescriptor>
            new() { LogicalName = "EntrySensor", IoType = IoPointType.DigitalInput },
            new() { LogicalName = "D1_Left", IoType = IoPointType.DigitalOutput },
            new() { LogicalName = "D1_Right", IoType = IoPointType.DigitalOutput }
        var vendorAddresses = mapper.MapIoPoints(ioPoints);
        Assert.Equal(3, vendorAddresses.Count);
        Assert.Contains(vendorAddresses, a => a.LogicalName == "EntrySensor");
        Assert.Contains(vendorAddresses, a => a.LogicalName == "D1_Left");
        Assert.Contains(vendorAddresses, a => a.LogicalName == "D1_Right");
    public void ValidateProfile_WithAllMappedPoints_ShouldReturnValid()
        var profile = new IoBindingProfile
            ProfileId = "PROFILE_001",
            ProfileName = "Test Profile",
            TopologyId = "TOPO_001",
            SensorBindings = new List<SensorBinding>
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
                        LogicalName = "D1_Left",
                        IoType = IoPointType.DigitalOutput
                    ActuatorType = ActuatorBindingType.DiverterLeft
        var (isValid, errorMessage) = mapper.ValidateProfile(profile);
        Assert.True(isValid);
        Assert.Null(errorMessage);
    public void ValidateProfile_WithUnmappedPoints_ShouldReturnInvalid()
                ["EntrySensor"] = new() { CardNumber = 0, BitNumber = 5 }
                        LogicalName = "UnmappedActuator",
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
        Assert.Contains("UnmappedActuator", errorMessage);
}
