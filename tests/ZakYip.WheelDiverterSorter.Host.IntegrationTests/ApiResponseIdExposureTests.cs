using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using ZakYip.WheelDiverterSorter.Host.Models.Config;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// 测试所有API响应不暴露LiteDB的内部Id字段
/// Tests to ensure no API endpoints expose LiteDB internal Id field
/// </summary>
/// <remarks>
/// 问题要求1: 确保所有Api端点未暴露LiteDB的Key
/// 
/// LiteDB使用int类型的Id作为内部主键，这个Id不应该暴露给API客户端。
/// API响应应该只包含业务相关的ID（如ChuteId, DiverterId等），不包含数据库内部的Id。
/// </remarks>
public class ApiResponseIdExposureTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ApiResponseIdExposureTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// 测试: 系统配置API响应不应包含Id字段
    /// </summary>
    [Fact]
    public async Task GetSystemConfig_ShouldNotExposeId()
    {
        // Act
        var response = await _client.GetAsync("/api/config/system");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var jsonString = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(jsonString);
        
        // 验证响应中不包含"id"或"Id"字段（不区分大小写）
        Assert.False(HasPropertyCaseInsensitive(jsonDocument.RootElement, "id"),
            "系统配置响应不应包含LiteDB的Id字段");
    }

    /// <summary>
    /// 测试: 通信配置API响应不应包含Id字段
    /// </summary>
    [Fact]
    public async Task GetCommunicationConfig_ShouldNotExposeId()
    {
        // Act
        var response = await _client.GetAsync("/api/communication/config/persisted");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var jsonString = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(jsonString);
        
        // 验证响应中不包含"id"或"Id"字段
        Assert.False(HasPropertyCaseInsensitive(jsonDocument.RootElement, "id"),
            "通信配置响应不应包含LiteDB的Id字段");
    }

    /// <summary>
    /// 测试: 格口路径拓扑配置响应不应包含Id字段
    /// </summary>
    [Fact]
    public async Task GetChutePathTopology_ShouldNotExposeId()
    {
        // Act
        var response = await _client.GetAsync("/api/config/topology");
        
        // Assert - 如果拓扑未配置，可能返回404，这也是正常的
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // 拓扑未配置，跳过测试
            return;
        }
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var jsonString = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(jsonString);
        
        // 验证响应中不包含"id"或"Id"字段（TopologyId等业务ID除外）
        // 我们检查根级别不应有名为"id"的字段
        if (jsonDocument.RootElement.TryGetProperty("Id", out _) || 
            jsonDocument.RootElement.TryGetProperty("id", out _))
        {
            // 如果有Id字段，确保它是TopologyId而不是纯粹的Id
            Assert.Fail("格口路径拓扑响应不应包含LiteDB的Id字段");
        }
    }

    /// <summary>
    /// 测试: 日志配置响应不应包含Id字段
    /// </summary>
    [Fact]
    public async Task GetLoggingConfig_ShouldNotExposeId()
    {
        // Act
        var response = await _client.GetAsync("/api/config/logging");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var jsonString = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(jsonString);
        
        // 验证响应中不包含"id"或"Id"字段
        Assert.False(HasPropertyCaseInsensitive(jsonDocument.RootElement, "id"),
            "日志配置响应不应包含LiteDB的Id字段");
    }

    /// <summary>
    /// 测试: IO联动配置响应不应包含Id字段
    /// </summary>
    [Fact]
    public async Task GetIoLinkageConfig_ShouldNotExposeId()
    {
        // Act
        var response = await _client.GetAsync("/api/iolinkage/config");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var jsonString = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(jsonString);
        
        // 验证响应中不包含"id"或"Id"字段
        Assert.False(HasPropertyCaseInsensitive(jsonDocument.RootElement, "id"),
            "IO联动配置响应不应包含LiteDB的Id字段");
    }

    /// <summary>
    /// 测试: 面板配置响应不应包含Id字段
    /// </summary>
    [Fact]
    public async Task GetPanelConfig_ShouldNotExposeId()
    {
        // Act
        var response = await _client.GetAsync("/api/panel/config");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var jsonString = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(jsonString);
        
        // 验证响应中不包含"id"或"Id"字段
        Assert.False(HasPropertyCaseInsensitive(jsonDocument.RootElement, "id"),
            "面板配置响应不应包含LiteDB的Id字段");
    }

    /// <summary>
    /// 测试: 摆轮配置响应不应包含Id字段
    /// </summary>
    [Fact]
    public async Task GetDiverterConfig_ShouldNotExposeId()
    {
        // Act
        var response = await _client.GetAsync("/api/hardware/diverters");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var jsonString = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(jsonString);
        
        // 验证响应中不包含"id"或"Id"字段
        // DiverterId是业务ID，不是LiteDB的Id，所以它是允许的
        // 我们只检查不应有名为"id"的字段（全小写）
        Assert.False(jsonDocument.RootElement.TryGetProperty("id", out _),
            "摆轮配置响应不应包含LiteDB的Id字段（小写id）");
    }

    /// <summary>
    /// 测试: 传感器配置响应不应包含Id字段
    /// </summary>
    [Fact]
    public async Task GetSensorConfig_ShouldNotExposeId()
    {
        // Act
        var response = await _client.GetAsync("/api/hardware/sensors");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var jsonString = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(jsonString);
        
        // 验证响应中不包含"id"字段（小写）
        Assert.False(jsonDocument.RootElement.TryGetProperty("id", out _),
            "传感器配置响应不应包含LiteDB的Id字段");
    }

    /// <summary>
    /// 辅助方法: 检查JSON元素是否包含指定名称的属性（不区分大小写）
    /// </summary>
    /// <param name="element">要检查的JSON元素</param>
    /// <param name="propertyName">属性名称</param>
    /// <returns>如果包含该属性则返回true，否则返回false</returns>
    private static bool HasPropertyCaseInsensitive(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            // 检查属性名是否为"id"（不区分大小写），但排除业务ID（如TopologyId, ChuteId等）
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                !property.Name.Contains("Id", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            
            // 如果属性名正好是"Id"（没有前缀），则认为是LiteDB的Id
            if (property.Name == "Id")
            {
                return true;
            }
        }

        return false;
    }
}
