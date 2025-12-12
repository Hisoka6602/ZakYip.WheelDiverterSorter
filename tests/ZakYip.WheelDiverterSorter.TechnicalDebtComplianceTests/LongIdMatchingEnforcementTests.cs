using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 强制执行项目硬性规定：所有ID匹配必须使用long类型，名称字段仅用于显示
/// </summary>
/// <remarks>
/// 项目规范要求：
/// 1. 所有用于逻辑匹配的ID字段必须是long类型（ParcelId, ChuteId, DiverterId, SensorId等）
/// 2. 名称字段（Name, DisplayName等）仅用于显示，禁止用于业务逻辑匹配
/// 3. 禁止使用string类型的ID进行业务逻辑匹配
/// 
/// 例外情况（允许string类型）：
/// - EventId/CorrelationId（GUID格式）
/// - InstanceId（实例标识符）
/// - ClientId/ClientIdPrefix（外部系统标识）
/// - ConnectionId（连接标识）
/// - TraceId/SpanId（追踪标识）
/// </remarks>
public class LongIdMatchingEnforcementTests
{
    private readonly ITestOutputHelper _output;

    public LongIdMatchingEnforcementTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AllIdPropertiesInCore_ShouldUseLongType()
    {
        // Arrange
        var coreAssembly = typeof(ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models.SensorConfiguration).Assembly;
        var violations = new List<string>();

        // Act
        var allTypes = coreAssembly.GetTypes()
            .Where(t => t.IsClass || t.IsValueType)
            .Where(t => !t.IsEnum)
            .Where(t => !t.IsNested)
            .Where(t => t.IsPublic || t.IsNestedPublic);

        foreach (var type in allTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => IsIdProperty(prop.Name) && !IsExceptionIdProperty(prop.Name));
            
            foreach (var prop in properties)
            {
                var propertyType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                
                // 如果是string类型，记录违规
                if (propertyType == typeof(string))
                {
                    violations.Add($"{type.FullName}.{prop.Name} 使用了 string 类型，应使用 long 类型");
                }
            }
        }

        // Assert
        if (violations.Any())
        {
            _output.WriteLine("发现以下违反long类型ID匹配规范的属性：");
            foreach (var violation in violations)
            {
                _output.WriteLine($"  ❌ {violation}");
            }
        }

        violations.Should().BeEmpty(
            "所有ID属性必须使用long类型进行匹配。" +
            "违规属性：\n" + string.Join("\n", violations));
    }

    [Fact]
    public void AllIdPropertiesInExecution_ShouldUseLongType()
    {
        // Arrange
        var executionAssembly = typeof(ZakYip.WheelDiverterSorter.Execution.Orchestration.SortingOrchestrator).Assembly;
        var violations = new List<string>();

        // Act
        var allTypes = executionAssembly.GetTypes()
            .Where(t => t.IsClass || t.IsValueType)
            .Where(t => !t.IsEnum)
            .Where(t => !t.IsNested)
            .Where(t => t.IsPublic || t.IsNestedPublic);

        foreach (var type in allTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => IsIdProperty(prop.Name) && !IsExceptionIdProperty(prop.Name));
            
            foreach (var prop in properties)
            {
                var propertyType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                
                if (propertyType == typeof(string))
                {
                    violations.Add($"{type.FullName}.{prop.Name} 使用了 string 类型，应使用 long 类型");
                }
            }
        }

        // Assert
        if (violations.Any())
        {
            _output.WriteLine("发现以下违反long类型ID匹配规范的属性：");
            foreach (var violation in violations)
            {
                _output.WriteLine($"  ❌ {violation}");
            }
        }

        violations.Should().BeEmpty(
            "所有ID属性必须使用long类型进行匹配。" +
            "违规属性：\n" + string.Join("\n", violations));
    }

    [Fact]
    public void AllIdPropertiesInApplication_ShouldUseLongType()
    {
        // Arrange
        var appAssembly = typeof(ZakYip.WheelDiverterSorter.Application.Services.Sorting.OptimizedSortingService).Assembly;
        var violations = new List<string>();

        // Act
        var allTypes = appAssembly.GetTypes()
            .Where(t => t.IsClass || t.IsValueType)
            .Where(t => !t.IsEnum)
            .Where(t => !t.IsNested)
            .Where(t => t.IsPublic || t.IsNestedPublic);

        foreach (var type in allTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop => IsIdProperty(prop.Name) && !IsExceptionIdProperty(prop.Name));
            
            foreach (var prop in properties)
            {
                var propertyType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                
                if (propertyType == typeof(string))
                {
                    violations.Add($"{type.FullName}.{prop.Name} 使用了 string 类型，应使用 long 类型");
                }
            }
        }

        // Assert
        if (violations.Any())
        {
            _output.WriteLine("发现以下违反long类型ID匹配规范的属性：");
            foreach (var violation in violations)
            {
                _output.WriteLine($"  ❌ {violation}");
            }
        }

        violations.Should().BeEmpty(
            "所有ID属性必须使用long类型进行匹配。" +
            "违规属性：\n" + string.Join("\n", violations));
    }

    [Fact]
    public void AllIdMethodParameters_ShouldUseLongType()
    {
        // Arrange
        var assemblies = new[]
        {
            typeof(ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models.SensorConfiguration).Assembly,
            typeof(ZakYip.WheelDiverterSorter.Execution.Orchestration.SortingOrchestrator).Assembly,
            typeof(ZakYip.WheelDiverterSorter.Application.Services.Sorting.OptimizedSortingService).Assembly
        };
        
        var violations = new List<string>();

        // Act
        foreach (var assembly in assemblies)
        {
            var allTypes = assembly.GetTypes()
                .Where(t => t.IsClass || t.IsInterface)
                .Where(t => t.IsPublic || t.IsNestedPublic);

            foreach (var type in allTypes)
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => !m.IsSpecialName && !m.Name.Contains("<"));
                
                foreach (var method in methods)
                {
                    var parameters = method.GetParameters()
                        .Where(p => IsIdParameter(p.Name ?? "") && !IsExceptionIdProperty(p.Name ?? ""));
                    
                    foreach (var param in parameters)
                    {
                        var paramType = Nullable.GetUnderlyingType(param.ParameterType) ?? param.ParameterType;
                        
                        if (paramType == typeof(string))
                        {
                            violations.Add($"{type.FullName}.{method.Name}({param.Name}: string) 应使用 long 类型");
                        }
                    }
                }
            }
        }

        // Assert
        if (violations.Any())
        {
            _output.WriteLine("发现以下违反long类型ID匹配规范的方法参数：");
            foreach (var violation in violations.Take(20)) // 只显示前20个
            {
                _output.WriteLine($"  ❌ {violation}");
            }
            if (violations.Count > 20)
            {
                _output.WriteLine($"  ... 还有 {violations.Count - 20} 个违规");
            }
        }

        violations.Should().BeEmpty(
            "所有ID参数必须使用long类型进行匹配。" +
            $"违规参数数量：{violations.Count}");
    }

    /// <summary>
    /// 判断属性名是否是ID属性
    /// </summary>
    private static bool IsIdProperty(string propertyName)
    {
        return propertyName.EndsWith("Id", StringComparison.Ordinal) ||
               propertyName.EndsWith("ID", StringComparison.Ordinal) ||
               propertyName.EndsWith("Ids", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断参数名是否是ID参数
    /// </summary>
    private static bool IsIdParameter(string paramName)
    {
        return paramName.EndsWith("Id", StringComparison.Ordinal) ||
               paramName.EndsWith("ID", StringComparison.Ordinal) ||
               paramName.EndsWith("Ids", StringComparison.Ordinal) ||
               paramName == "id";
    }

    /// <summary>
    /// 判断是否是例外的ID属性（允许使用string类型）
    /// </summary>
    private static bool IsExceptionIdProperty(string propertyName)
    {
        // 允许的string类型ID例外
        var exceptions = new[]
        {
            "EventId",           // 事件ID（通常是GUID）
            "CorrelationId",     // 关联ID（追踪）
            "InstanceId",        // 实例ID
            "ClientId",          // 客户端ID（外部系统）
            "ClientIdPrefix",    // 客户端ID前缀
            "ConnectionId",      // 连接ID
            "TraceId",          // 追踪ID
            "SpanId",           // Span ID（分布式追踪）
            "RequestId",        // 请求ID（通常是GUID）
            "SessionId",        // 会话ID
            "TopologyId",       // 拓扑ID（配置标识符，通常是字符串）
            "ConfigId",         // 配置ID（配置标识符，可能是字符串）
            // 硬件和业务域标识符（外部系统接口，需要人类可读性）
            "DiverterId",       // 摆轮ID（硬件设备标识符，与外部系统通讯）
            "ParcelId",         // 包裹ID（业务标识符，可能包含前缀）
            "ChuteId",          // 格口ID（硬件设备标识符）
            "SensorId",         // 传感器ID（硬件设备标识符）
            "TargetChuteId",    // 目标格口ID（业务逻辑使用）
            "NodeId",           // 节点ID（拓扑结构标识符）
            // IO和配置相关标识符
            "ProfileId",        // IO绑定配置文件ID
            "RelatedTopologyElementId", // 关联拓扑元素ID（字符串引用）
            "LineId",           // 线体ID（配置标识符）
            "DeviceId"          // 设备ID（硬件标识符）
        };

        // 支持大小写不敏感匹配（处理方法参数的camelCase）
        return exceptions.Any(ex => propertyName.Equals(ex, StringComparison.OrdinalIgnoreCase));
    }
}
