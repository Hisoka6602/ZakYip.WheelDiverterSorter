using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Moq;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Services.Config;
using ZakYip.WheelDiverterSorter.Application.Services.Caching;
using ZakYip.WheelDiverterSorter.Application.Services.Sorting;
using ZakYip.WheelDiverterSorter.Application.Services.Metrics;
using ZakYip.WheelDiverterSorter.Application.Services.Topology;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Observability.ConfigurationAudit;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests;

/// <summary>
/// 日志配置服务单元测试
/// </summary>
public class LoggingConfigServiceTests
{
    private readonly Mock<ILoggingConfigurationRepository> _mockRepository;
    private readonly Mock<ISlidingConfigCache> _mockConfigCache;
    private readonly Mock<ILogger<LoggingConfigService>> _mockLogger;
    private readonly Mock<IConfigurationAuditLogger> _mockAuditLogger;
    private readonly LoggingConfigService _service;
    private readonly LoggingConfiguration _defaultConfig;

    public LoggingConfigServiceTests()
    {
        _mockRepository = new Mock<ILoggingConfigurationRepository>();
        _mockConfigCache = new Mock<ISlidingConfigCache>();
        _mockLogger = new Mock<ILogger<LoggingConfigService>>();
        _mockAuditLogger = new Mock<IConfigurationAuditLogger>();

        _defaultConfig = LoggingConfiguration.GetDefault();
        _mockRepository.Setup(r => r.Get()).Returns(_defaultConfig);
        
        // Setup cache to delegate to repository for GetOrAdd
        _mockConfigCache.Setup(c => c.GetOrAdd(It.IsAny<object>(), It.IsAny<Func<LoggingConfiguration>>()))
            .Returns((object key, Func<LoggingConfiguration> factory) => factory());

        _service = new LoggingConfigService(
            _mockRepository.Object,
            _mockConfigCache.Object,
            _mockLogger.Object,
            _mockAuditLogger.Object);
    }

    #region 获取配置测试

    [Fact]
    public void GetLoggingConfig_ShouldReturnConfigFromRepository()
    {
        // Arrange
        var expectedConfig = new LoggingConfiguration
        {
            Id = 1,
            EnableParcelLifecycleLog = true,
            EnableParcelTraceLog = false,
            EnablePathExecutionLog = true,
            EnableCommunicationLog = false,
            EnableDriverLog = true,
            EnablePerformanceLog = false,
            EnableAlarmLog = true,
            EnableDebugLog = false,
            Version = 2
        };
        _mockRepository.Setup(r => r.Get()).Returns(expectedConfig);

        // Act
        var result = _service.GetLoggingConfig();

        // Assert
        Assert.Equal(expectedConfig.Id, result.Id);
        Assert.Equal(expectedConfig.EnableParcelLifecycleLog, result.EnableParcelLifecycleLog);
        Assert.Equal(expectedConfig.EnableParcelTraceLog, result.EnableParcelTraceLog);
        Assert.Equal(expectedConfig.EnablePathExecutionLog, result.EnablePathExecutionLog);
        Assert.Equal(expectedConfig.EnableCommunicationLog, result.EnableCommunicationLog);
        Assert.Equal(expectedConfig.EnableDriverLog, result.EnableDriverLog);
        Assert.Equal(expectedConfig.EnablePerformanceLog, result.EnablePerformanceLog);
        Assert.Equal(expectedConfig.EnableAlarmLog, result.EnableAlarmLog);
        Assert.Equal(expectedConfig.EnableDebugLog, result.EnableDebugLog);
        Assert.Equal(expectedConfig.Version, result.Version);
        _mockRepository.Verify(r => r.Get(), Times.Once);
    }

    [Fact]
    public void GetDefaultTemplate_ShouldReturnDefaultConfig()
    {
        // Act
        var result = _service.GetDefaultTemplate();

        // Assert
        Assert.True(result.EnableParcelLifecycleLog);
        Assert.True(result.EnableParcelTraceLog);
        Assert.True(result.EnablePathExecutionLog);
        Assert.True(result.EnableCommunicationLog);
        Assert.True(result.EnableDriverLog);
        Assert.True(result.EnablePerformanceLog);
        Assert.True(result.EnableAlarmLog);
        Assert.False(result.EnableDebugLog); // Debug默认关闭
        Assert.Equal("logging", result.ConfigName);
    }

    #endregion

    #region 更新配置测试

    [Fact]
    public async Task UpdateLoggingConfigAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var request = new UpdateLoggingConfigCommand
        {
            EnableParcelLifecycleLog = false,
            EnableParcelTraceLog = true,
            EnablePathExecutionLog = false,
            EnableCommunicationLog = true,
            EnableDriverLog = false,
            EnablePerformanceLog = true,
            EnableAlarmLog = false,
            EnableDebugLog = true
        };

        var updatedConfig = new LoggingConfiguration
        {
            Id = 1,
            EnableParcelLifecycleLog = false,
            EnableParcelTraceLog = true,
            EnablePathExecutionLog = false,
            EnableCommunicationLog = true,
            EnableDriverLog = false,
            EnablePerformanceLog = true,
            EnableAlarmLog = false,
            EnableDebugLog = true,
            Version = 2
        };

        _mockRepository.Setup(r => r.Update(It.IsAny<LoggingConfiguration>()));
        _mockRepository.Setup(r => r.Get()).Returns(updatedConfig);

        // Act
        var result = await _service.UpdateLoggingConfigAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.UpdatedConfig);
        Assert.Equal(request.EnableParcelLifecycleLog, result.UpdatedConfig.EnableParcelLifecycleLog);
        Assert.Equal(request.EnableParcelTraceLog, result.UpdatedConfig.EnableParcelTraceLog);
        Assert.Equal(request.EnablePathExecutionLog, result.UpdatedConfig.EnablePathExecutionLog);
        Assert.Equal(request.EnableCommunicationLog, result.UpdatedConfig.EnableCommunicationLog);
        Assert.Equal(request.EnableDriverLog, result.UpdatedConfig.EnableDriverLog);
        Assert.Equal(request.EnablePerformanceLog, result.UpdatedConfig.EnablePerformanceLog);
        Assert.Equal(request.EnableAlarmLog, result.UpdatedConfig.EnableAlarmLog);
        Assert.Equal(request.EnableDebugLog, result.UpdatedConfig.EnableDebugLog);
        _mockRepository.Verify(r => r.Update(It.IsAny<LoggingConfiguration>()), Times.Once);
    }

    [Fact]
    public async Task UpdateLoggingConfigAsync_WhenExceptionThrown_ShouldReturnFailure()
    {
        // Arrange
        var request = new UpdateLoggingConfigCommand();
        _mockRepository.Setup(r => r.Update(It.IsAny<LoggingConfiguration>()))
            .Throws(new Exception("数据库错误"));

        // Act
        var result = await _service.UpdateLoggingConfigAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("更新日志配置失败", result.ErrorMessage);
        Assert.Null(result.UpdatedConfig);
    }

    #endregion

    #region 重置配置测试

    [Fact]
    public async Task ResetLoggingConfigAsync_ShouldResetToDefaults()
    {
        // Arrange
        var defaultConfig = LoggingConfiguration.GetDefault();
        defaultConfig.Id = 1;
        defaultConfig.Version = 1;

        _mockRepository.Setup(r => r.Update(It.IsAny<LoggingConfiguration>()));
        _mockRepository.Setup(r => r.Get()).Returns(defaultConfig);

        // Act
        var result = await _service.ResetLoggingConfigAsync();

        // Assert
        Assert.True(result.EnableParcelLifecycleLog);
        Assert.True(result.EnableParcelTraceLog);
        Assert.True(result.EnablePathExecutionLog);
        Assert.True(result.EnableCommunicationLog);
        Assert.True(result.EnableDriverLog);
        Assert.True(result.EnablePerformanceLog);
        Assert.True(result.EnableAlarmLog);
        Assert.False(result.EnableDebugLog);
        _mockRepository.Verify(r => r.Update(It.IsAny<LoggingConfiguration>()), Times.Once);
        _mockRepository.Verify(r => r.Get(), Times.Exactly(2)); // Called once for before config, once for after
    }

    #endregion

    #region 禁用所有日志测试

    [Fact]
    public async Task UpdateLoggingConfigAsync_DisableAllLogs_ShouldSucceed()
    {
        // Arrange
        var request = new UpdateLoggingConfigCommand
        {
            EnableParcelLifecycleLog = false,
            EnableParcelTraceLog = false,
            EnablePathExecutionLog = false,
            EnableCommunicationLog = false,
            EnableDriverLog = false,
            EnablePerformanceLog = false,
            EnableAlarmLog = false,
            EnableDebugLog = false
        };

        var updatedConfig = new LoggingConfiguration
        {
            Id = 1,
            EnableParcelLifecycleLog = false,
            EnableParcelTraceLog = false,
            EnablePathExecutionLog = false,
            EnableCommunicationLog = false,
            EnableDriverLog = false,
            EnablePerformanceLog = false,
            EnableAlarmLog = false,
            EnableDebugLog = false,
            Version = 2
        };

        _mockRepository.Setup(r => r.Update(It.IsAny<LoggingConfiguration>()));
        _mockRepository.Setup(r => r.Get()).Returns(updatedConfig);

        // Act
        var result = await _service.UpdateLoggingConfigAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.UpdatedConfig);
        Assert.False(result.UpdatedConfig.EnableParcelLifecycleLog);
        Assert.False(result.UpdatedConfig.EnableParcelTraceLog);
        Assert.False(result.UpdatedConfig.EnablePathExecutionLog);
        Assert.False(result.UpdatedConfig.EnableCommunicationLog);
        Assert.False(result.UpdatedConfig.EnableDriverLog);
        Assert.False(result.UpdatedConfig.EnablePerformanceLog);
        Assert.False(result.UpdatedConfig.EnableAlarmLog);
        Assert.False(result.UpdatedConfig.EnableDebugLog);
    }

    #endregion
}
