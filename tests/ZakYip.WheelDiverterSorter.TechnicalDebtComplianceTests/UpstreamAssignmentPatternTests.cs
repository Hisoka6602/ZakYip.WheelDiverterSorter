using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Reflection;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-UPSTREAM02: 测试验证上游交互模式符合"检测通知 + 异步格口推送 + 落格完成通知"规范
/// </summary>
public class UpstreamAssignmentPatternTests
{
    private static readonly string[] SourceAssemblies =
    {
        "ZakYip.WheelDiverterSorter.Core",
        "ZakYip.WheelDiverterSorter.Execution",
        "ZakYip.WheelDiverterSorter.Communication",
        "ZakYip.WheelDiverterSorter.Ingress",
        "ZakYip.WheelDiverterSorter.Observability",
        "ZakYip.WheelDiverterSorter.Host",
        "ZakYip.WheelDiverterSorter.Simulation"
    };

    /// <summary>
    /// 验证仓库中不存在 ChuteAssignmentRequest 类型
    /// </summary>
    [Fact]
    public void ShouldNotContainChuteAssignmentRequestType()
    {
        // Arrange
        var assemblies = GetLoadedAssemblies();
        var violations = new List<string>();

        // Act
        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.Name.Contains("ChuteAssignmentRequest", StringComparison.OrdinalIgnoreCase));

                foreach (var type in types)
                {
                    violations.Add($"发现 ChuteAssignmentRequest 类型: {type.FullName} in {assembly.GetName().Name}");
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // 忽略无法加载的类型
            }
        }

        // Assert
        Assert.Empty(violations);
    }

    /// <summary>
    /// 验证 IUpstreamRoutingClient 接口包含正确的方法
    /// </summary>
    [Fact]
    public void IUpstreamRoutingClient_ShouldHaveCorrectMethods()
    {
        // Arrange
        var coreAssembly = GetLoadedAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "ZakYip.WheelDiverterSorter.Core");

        Assert.NotNull(coreAssembly);

        var interfaceType = coreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "IUpstreamRoutingClient");

        Assert.NotNull(interfaceType);

        // Act & Assert - 验证方法存在
        var methods = interfaceType.GetMethods();

        // 验证 NotifyParcelDetectedAsync 存在
        var notifyParcelMethod = methods.FirstOrDefault(m => m.Name == "NotifyParcelDetectedAsync");
        Assert.NotNull(notifyParcelMethod);

        // 验证 NotifySortingCompletedAsync 存在
        var notifySortingMethod = methods.FirstOrDefault(m => m.Name == "NotifySortingCompletedAsync");
        Assert.NotNull(notifySortingMethod);

        // 验证 ChuteAssigned 事件存在
        var chuteAssignedEvent = interfaceType.GetEvent("ChuteAssigned");
        Assert.NotNull(chuteAssignedEvent);

        // 验证不存在 RequestChuteAssignmentAsync
        var requestMethod = methods.FirstOrDefault(m => m.Name.Contains("RequestChuteAssignment"));
        Assert.Null(requestMethod);

        // 验证不存在 ChuteAssignmentReceived
        var oldEvent = interfaceType.GetEvent("ChuteAssignmentReceived");
        Assert.Null(oldEvent);
    }

    /// <summary>
    /// 验证 ChuteAssignmentEventArgs 包含正确的属性
    /// </summary>
    [Fact]
    public void ChuteAssignmentEventArgs_ShouldHaveCorrectProperties()
    {
        // Arrange
        var coreAssembly = GetLoadedAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "ZakYip.WheelDiverterSorter.Core");

        Assert.NotNull(coreAssembly);

        var eventArgsType = coreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "ChuteAssignmentEventArgs");

        Assert.NotNull(eventArgsType);

        // Act
        var properties = eventArgsType.GetProperties();

        // Assert
        // 验证必要属性存在
        Assert.Contains(properties, p => p.Name == "ParcelId");
        Assert.Contains(properties, p => p.Name == "ChuteId");
        Assert.Contains(properties, p => p.Name == "AssignedAt");
        Assert.Contains(properties, p => p.Name == "DwsPayload");

        // 验证旧属性不存在
        Assert.DoesNotContain(properties, p => p.Name == "NotificationTime");
    }

    /// <summary>
    /// 验证 SortingCompletedNotification 存在且包含正确的属性
    /// </summary>
    [Fact]
    public void SortingCompletedNotification_ShouldExistWithCorrectProperties()
    {
        // Arrange
        var coreAssembly = GetLoadedAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "ZakYip.WheelDiverterSorter.Core");

        Assert.NotNull(coreAssembly);

        var notificationType = coreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "SortingCompletedNotification");

        Assert.NotNull(notificationType);

        // Act
        var properties = notificationType.GetProperties();

        // Assert
        Assert.Contains(properties, p => p.Name == "ParcelId");
        Assert.Contains(properties, p => p.Name == "ActualChuteId");
        Assert.Contains(properties, p => p.Name == "CompletedAt");
        Assert.Contains(properties, p => p.Name == "IsSuccess");
        Assert.Contains(properties, p => p.Name == "FailureReason");
    }

    /// <summary>
    /// 验证 DwsMeasurement 值对象存在
    /// </summary>
    [Fact]
    public void DwsMeasurement_ShouldExist()
    {
        // Arrange
        var coreAssembly = GetLoadedAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "ZakYip.WheelDiverterSorter.Core");

        Assert.NotNull(coreAssembly);

        // Act
        var dwsMeasurementType = coreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "DwsMeasurement");

        // Assert
        Assert.NotNull(dwsMeasurementType);
        Assert.True(dwsMeasurementType.IsValueType, "DwsMeasurement 应该是值类型（struct）");
    }

    /// <summary>
    /// 扫描源代码中不应存在 ChuteAssignmentRequest 引用
    /// </summary>
    [Fact]
    public void SourceCode_ShouldNotReferenceChuteAssignmentRequest()
    {
        // Arrange
        var srcPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentStateomain.BaseDirectory,
            "..", "..", "..", "..", "..", "src"));

        if (!Directory.Exists(srcPath))
        {
            // 如果路径不存在，跳过测试
            return;
        }

        var violations = new List<string>();

        // Act
        var csFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj") && !f.Contains("bin"));

        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // 跳过注释行
                var trimmedLine = line.TrimStart();
                if (trimmedLine.StartsWith("//") || trimmedLine.StartsWith("*") || trimmedLine.StartsWith("///"))
                {
                    continue;
                }
                
                if (line.Contains("ChuteAssignmentRequest", StringComparison.Ordinal))
                {
                    violations.Add($"发现 ChuteAssignmentRequest 引用: {file}:{i + 1}");
                }
            }
        }

        // Assert
        Assert.Empty(violations);
    }

    /// <summary>
    /// 验证 IUpstreamRoutingClient 不包含 RequestChuteAssignment 方法
    /// </summary>
    /// <remarks>
    /// 注意：这只检查 IUpstreamRoutingClient 接口，不检查其他可能存在的 ISortingDecisionService 等高层抽象
    /// </remarks>
    [Fact]
    public void IUpstreamRoutingClient_ShouldNotHaveRequestChuteAssignmentMethod()
    {
        // Arrange
        var coreAssembly = GetLoadedAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "ZakYip.WheelDiverterSorter.Core");

        Assert.NotNull(coreAssembly);

        var interfaceType = coreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "IUpstreamRoutingClient");

        Assert.NotNull(interfaceType);

        // Act
        var methods = interfaceType.GetMethods();

        // Assert - 验证不存在 RequestChuteAssignment 方法
        var requestMethod = methods.FirstOrDefault(m => m.Name.Contains("RequestChuteAssignment"));
        Assert.Null(requestMethod);
    }

    private static IEnumerable<Assembly> GetLoadedAssemblies()
    {
        // 尝试加载所有相关程序集
        var assemblies = new List<Assembly>();
        foreach (var assemblyName in SourceAssemblies)
        {
            try
            {
                var assembly = Assembly.Load(assemblyName);
                if (assembly != null)
                {
                    assemblies.Add(assembly);
                }
            }
            catch
            {
                // 忽略无法加载的程序集
            }
        }
        return assemblies;
    }
}
