using System.ComponentModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class AlertSeverityTests
{
    [Fact]
    public void AlertSeverity_EnumValues_ShouldBeCorrect()
    {
        // Assert
        Assert.Equal(0, (int)AlertSeverity.Info);
        Assert.Equal(1, (int)AlertSeverity.Warning);
        Assert.Equal(2, (int)AlertSeverity.Critical);
    }

    [Fact]
    public void AlertSeverity_ShouldHaveDescriptionAttributes()
    {
        // Arrange & Act
        var infoDescription = GetDescription(AlertSeverity.Info);
        var warningDescription = GetDescription(AlertSeverity.Warning);
        var criticalDescription = GetDescription(AlertSeverity.Critical);

        // Assert
        Assert.Equal("信息", infoDescription);
        Assert.Equal("警告", warningDescription);
        Assert.Equal("严重", criticalDescription);
    }

    [Fact]
    public void AlertSeverity_ToString_ShouldReturnCorrectName()
    {
        // Arrange & Act & Assert
        Assert.Equal("Info", AlertSeverity.Info.ToString());
        Assert.Equal("Warning", AlertSeverity.Warning.ToString());
        Assert.Equal("Critical", AlertSeverity.Critical.ToString());
    }

    private static string GetDescription(AlertSeverity severity)
    {
        var type = severity.GetType();
        var memberInfo = type.GetMember(severity.ToString());
        var attributes = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
        return ((DescriptionAttribute)attributes[0]).Description;
    }
}
