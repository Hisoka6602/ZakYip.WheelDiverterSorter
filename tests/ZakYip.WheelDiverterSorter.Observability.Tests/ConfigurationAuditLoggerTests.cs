using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Moq;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.ConfigurationAudit;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

/// <summary>
/// 配置审计日志服务单元测试
/// </summary>
public class ConfigurationAuditLoggerTests
{
    private readonly Mock<ILogger<ConfigurationAuditLogger>> _mockLogger;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly ConfigurationAuditLogger _auditLogger;

    public ConfigurationAuditLoggerTests()
    {
        _mockLogger = new Mock<ILogger<ConfigurationAuditLogger>>();
        _mockClock = new Mock<ISystemClock>();
        _mockClock.Setup(c => c.LocalNow).Returns(new DateTime(2025, 12, 5, 13, 15, 23));
        _auditLogger = new ConfigurationAuditLogger(_mockLogger.Object, _mockClock.Object);
    }

    [Fact]
    public void LogConfigurationChange_WithValidConfig_ShouldLogInformation()
    {
        // Arrange
        var beforeConfig = new SystemConfiguration { ExceptionChuteId = 999 };
        var afterConfig = new SystemConfiguration { ExceptionChuteId = 888 };

        // Act
        _auditLogger.LogConfigurationChange(
            configName: "SystemConfiguration",
            operationType: "Update",
            beforeConfig: beforeConfig,
            afterConfig: afterConfig);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[配置审计]") && v.ToString()!.Contains("SystemConfiguration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogConfigurationChange_WithNullBefore_ShouldLogInformation()
    {
        // Arrange
        var afterConfig = new SystemConfiguration { ExceptionChuteId = 888 };

        // Act
        _auditLogger.LogConfigurationChange<SystemConfiguration>(
            configName: "SystemConfiguration",
            operationType: "Create",
            beforeConfig: null,
            afterConfig: afterConfig);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[配置审计]") && v.ToString()!.Contains("Create")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogConfigurationChange_WithOperatorInfo_ShouldIncludeOperator()
    {
        // Arrange
        var beforeConfig = new SystemConfiguration { ExceptionChuteId = 999 };
        var afterConfig = new SystemConfiguration { ExceptionChuteId = 888 };

        // Act
        _auditLogger.LogConfigurationChange(
            configName: "SystemConfiguration",
            operationType: "Update",
            beforeConfig: beforeConfig,
            afterConfig: afterConfig,
            operatorInfo: "admin@localhost");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Operator=admin@localhost")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogConfigurationChange_WithEmptyConfigName_ShouldThrowArgumentException()
    {
        // Arrange
        var config = new SystemConfiguration { ExceptionChuteId = 999 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _auditLogger.LogConfigurationChange(
                configName: "",
                operationType: "Update",
                beforeConfig: config,
                afterConfig: config));
    }

    [Fact]
    public void LogConfigurationChange_WithEmptyOperationType_ShouldThrowArgumentException()
    {
        // Arrange
        var config = new SystemConfiguration { ExceptionChuteId = 999 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _auditLogger.LogConfigurationChange(
                configName: "SystemConfiguration",
                operationType: "",
                beforeConfig: config,
                afterConfig: config));
    }
}
