using System.Reflection;
using Xunit;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-UPSTREAM01: 确保不存在 HTTP 上游协议实现
/// </summary>
/// <remarks>
/// <para>这些测试用于确保 HTTP 协议实现已被彻底移除：</para>
/// <list type="number">
///   <item>Communication/Clients 目录下不存在 Http 相关类型</item>
///   <item>CommunicationMode 枚举不包含 Http 值</item>
///   <item>RuleEngineConnectionOptions 不包含 HttpApi 属性</item>
/// </list>
/// </remarks>
public class UpstreamProtocolTests
{
    /// <summary>
    /// 获取 Communication 程序集
    /// </summary>
    private static readonly Assembly CommunicationAssembly = typeof(RuleEngineConnectionOptions).Assembly;

    /// <summary>
    /// 获取 Core 程序集
    /// </summary>
    private static readonly Assembly CoreAssembly = typeof(CommunicationMode).Assembly;

    /// <summary>
    /// 辅助方法：验证类型不包含指定属性
    /// </summary>
    private static void AssertTypeDoesNotHaveProperty(Type type, string propertyName)
    {
        var property = type.GetProperty(propertyName);
        Assert.Null(property);
    }

    [Fact]
    public void Communication_Clients_ShouldNotHaveHttpTypes()
    {
        // Arrange - 查找 Communication.Clients 命名空间下的所有类型
        var clientTypes = CommunicationAssembly
            .GetTypes()
            .Where(t => t.Namespace?.Contains("Communication.Clients") == true)
            .Where(t => t.Name.Contains("Http", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.FullName)
            .ToList();

        // Assert - 不应存在 Http 相关类型
        Assert.Empty(clientTypes);
    }

    [Fact]
    public void Communication_Gateways_ShouldNotHaveHttpTypes()
    {
        // Arrange - 查找 Communication.Gateways 命名空间下的所有类型
        var gatewayTypes = CommunicationAssembly
            .GetTypes()
            .Where(t => t.Namespace?.Contains("Communication.Gateways") == true)
            .Where(t => t.Name.Contains("Http", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.FullName)
            .ToList();

        // Assert - 不应存在 Http 相关类型
        Assert.Empty(gatewayTypes);
    }

    [Fact]
    public void Communication_Configuration_ShouldNotHaveHttpOptions()
    {
        // Arrange - 查找 Communication.Configuration 命名空间下的 HttpOptions 类型
        var httpOptionsTypes = CommunicationAssembly
            .GetTypes()
            .Where(t => t.Namespace?.Contains("Communication.Configuration") == true)
            .Where(t => t.Name == "HttpOptions")
            .Select(t => t.FullName)
            .ToList();

        // Assert - 不应存在 HttpOptions 类型
        Assert.Empty(httpOptionsTypes);
    }

    [Fact]
    public void CommunicationMode_ShouldNotContainHttp()
    {
        // Arrange - 获取 CommunicationMode 枚举的所有值
        var enumValues = Enum.GetNames(typeof(CommunicationMode));

        // Assert - 不应包含 Http 值
        Assert.DoesNotContain("Http", enumValues);
    }

    [Fact]
    public void CommunicationMode_ShouldOnlyContainTcpSignalRMqtt()
    {
        // Arrange - 获取 CommunicationMode 枚举的所有值
        var enumValues = Enum.GetNames(typeof(CommunicationMode));
        var allowedValues = new[] { "Tcp", "SignalR", "Mqtt" };

        // Assert - 应只包含 Tcp, SignalR, Mqtt
        foreach (var value in enumValues)
        {
            Assert.Contains(value, allowedValues);
        }
    }

    [Fact]
    public void RuleEngineConnectionOptions_ShouldNotHaveHttpApiProperty()
    {
        // Assert - 使用 typeof() 确保编译时类型安全
        AssertTypeDoesNotHaveProperty(typeof(RuleEngineConnectionOptions), "HttpApi");
    }

    [Fact]
    public void RuleEngineConnectionOptions_ShouldNotHaveHttpProperty()
    {
        // Assert - 使用 typeof() 确保编译时类型安全
        AssertTypeDoesNotHaveProperty(typeof(RuleEngineConnectionOptions), "Http");
    }

    [Fact]
    public void CommunicationConfiguration_ShouldNotHaveHttpApiProperty()
    {
        // Assert - 使用 typeof() 确保编译时类型安全
        AssertTypeDoesNotHaveProperty(typeof(CommunicationConfiguration), "HttpApi");
    }

    [Fact]
    public void CommunicationConfiguration_ShouldNotHaveHttpConfigProperty()
    {
        // Assert - 使用 typeof() 确保编译时类型安全
        AssertTypeDoesNotHaveProperty(typeof(CommunicationConfiguration), "Http");
    }

    [Fact]
    public void Core_ShouldNotHaveHttpConfigType()
    {
        // Arrange - 查找 Core 程序集中的 HttpConfig 类型
        var httpConfigTypes = CoreAssembly
            .GetTypes()
            .Where(t => t.Name == "HttpConfig")
            .Select(t => t.FullName)
            .ToList();

        // Assert - 不应存在 HttpConfig 类型
        Assert.Empty(httpConfigTypes);
    }
}
