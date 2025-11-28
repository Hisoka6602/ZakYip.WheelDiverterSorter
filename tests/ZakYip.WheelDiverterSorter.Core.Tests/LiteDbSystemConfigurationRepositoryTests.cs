using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class LiteDbSystemConfigurationRepositoryTests : IDisposable
{
    private readonly string _testDatabasePath;
    private readonly LiteDbSystemConfigurationRepository _repository;

    public LiteDbSystemConfigurationRepositoryTests()
    {
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_system_config_{Guid.NewGuid()}.db");
        var mockClock = new Moq.Mock<Utilities.ISystemClock>();
        mockClock.Setup(c => c.LocalNow).Returns(DateTime.Now);
        _repository = new LiteDbSystemConfigurationRepository(_testDatabasePath, mockClock.Object);
    }

    public void Dispose()
    {
        // Clean up test database
        if (File.Exists(_testDatabasePath))
        {
            File.Delete(_testDatabasePath);
        }
    }

    [Fact]
    public void InitializeDefault_CreatesDefaultConfiguration()
    {
        // Act
        _repository.InitializeDefault();
        var config = _repository.Get();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("system", config.ConfigName);
        Assert.Equal(999, config.ExceptionChuteId);
        Assert.Equal(SortingMode.Formal, config.SortingMode);
    }

    [Fact]
    public void InitializeDefault_CalledTwice_DoesNotDuplicate()
    {
        // Act
        _repository.InitializeDefault();
        _repository.InitializeDefault();
        var config = _repository.Get();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(1, config.Version);
    }

    [Fact]
    public void Get_WithoutInitialization_ReturnsDefaultConfig()
    {
        // Act
        var config = _repository.Get();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("system", config.ConfigName);
        Assert.Equal(999, config.ExceptionChuteId);
    }

    [Fact]
    public void Update_WithValidConfiguration_UpdatesSuccessfully()
    {
        // Arrange
        _repository.InitializeDefault();
        var newConfig = SystemConfiguration.GetDefault();
        newConfig.ExceptionChuteId = 888;
        newConfig.SortingMode = SortingMode.FixedChute;
        newConfig.FixedChuteId = 123;

        // Act
        _repository.Update(newConfig);
        var updated = _repository.Get();

        // Assert
        Assert.Equal(888, updated.ExceptionChuteId);
        Assert.Equal(SortingMode.FixedChute, updated.SortingMode);
        Assert.Equal(123, updated.FixedChuteId);
        Assert.Equal(2, updated.Version); // Version should increment
    }

    [Fact]
    public void Update_WithInvalidConfiguration_ThrowsArgumentException()
    {
        // Arrange
        var invalidConfig = SystemConfiguration.GetDefault();
        invalidConfig.ExceptionChuteId = 0; // Invalid

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _repository.Update(invalidConfig));
        Assert.Contains("异常格口ID必须大于0", exception.Message);
    }

    [Fact]
    public void Update_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _repository.Update(null!));
    }

    [Fact]
    public void Update_MultipleTimes_IncrementsVersion()
    {
        // Arrange
        _repository.InitializeDefault();
        
        // Act & Assert
        for (int i = 1; i <= 5; i++)
        {
            var config = SystemConfiguration.GetDefault();
            config.ExceptionChuteId = 900 + i;
            _repository.Update(config);
            
            var updated = _repository.Get();
            Assert.Equal(i + 1, updated.Version); // Version increments each time
            Assert.Equal(900 + i, updated.ExceptionChuteId);
        }
    }

    [Fact]
    public void Update_PreservesCreatedAt()
    {
        // Arrange
        _repository.InitializeDefault();
        var original = _repository.Get();
        var originalCreatedAt = original.CreatedAt;
        
        // Wait a bit to ensure time difference
        Thread.Sleep(100);

        // Act
        var newConfig = SystemConfiguration.GetDefault();
        newConfig.ExceptionChuteId = 888;
        _repository.Update(newConfig);
        var updated = _repository.Get();

        // Assert
        Assert.Equal(originalCreatedAt, updated.CreatedAt);
        Assert.NotEqual(originalCreatedAt, updated.UpdatedAt);
    }
}
