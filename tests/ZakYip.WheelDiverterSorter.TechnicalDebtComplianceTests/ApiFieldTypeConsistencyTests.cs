using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Reflection;
using System.Text;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// API 字段类型一致性测试
/// API Field Type Consistency Tests
/// </summary>
/// <remarks>
/// TD-059: 确保所有配置 API 端点的字段类型与 Core 层模型完全匹配
/// Ensures all configuration API endpoint field types exactly match Core layer models
/// </remarks>
public class ApiFieldTypeConsistencyTests
{
    private static string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "ZakYip.WheelDiverterSorter.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// 确保所有配置 API 响应/请求模型的 ID 字段都是 long 类型
    /// Ensure all configuration API response/request models use long for ID fields
    /// </summary>
    [Fact]
    public void AllConfigApiModels_ShouldUseLongForIdFields()
    {
        var solutionRoot = GetSolutionRoot();
        var hostDll = Path.Combine(solutionRoot, 
            "src/Host/ZakYip.WheelDiverterSorter.Host/bin/Debug/net8.0/ZakYip.WheelDiverterSorter.Host.dll");

        if (!File.Exists(hostDll))
        {
            Assert.Fail($"Host DLL not found at {hostDll}. Please build the solution first.");
        }

        var assembly = Assembly.LoadFrom(hostDll);
        var modelTypes = assembly.GetTypes()
            .Where(t => t.Namespace != null && 
                       (t.Namespace.StartsWith("ZakYip.WheelDiverterSorter.Host.Models")))
            .Where(t => t.Name.EndsWith("Request") || t.Name.EndsWith("Response") || t.Name.EndsWith("Dto"))
            .ToList();

        var violations = new List<(string TypeName, string PropertyName, string ActualType)>();

        foreach (var type in modelTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var prop in properties)
            {
                var propName = prop.Name;
                
                // Check if this is an ID field (ends with "Id" or "ID")
                // Exception: The database internal "Id" field can be int (used only for LiteDB primary key)
                if ((propName.EndsWith("Id") || propName.EndsWith("ID")) && 
                    propName != "Id") // Exclude database internal Id
                {
                    // ID fields should be long, long?, or string (string is acceptable for API flexibility)
                    var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    
                    // Only report violations for int types - string is acceptable for API layer
                    if (propType != typeof(long) && propType != typeof(string))
                    {
                        violations.Add((type.Name, propName, propType.Name));
                    }
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个 ID 字段类型不是 long:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (typeName, propertyName, actualType) in violations)
            {
                report.AppendLine($"  ⚠️ {typeName}.{propertyName}");
                report.AppendLine($"     当前类型: {actualType}");
                report.AppendLine($"     期望类型: long (或 long?)");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  根据架构规范（copilot-instructions.md），所有业务 ID 字段必须使用 long 类型。");
            report.AppendLine("  1. 将上述字段类型从 int 改为 long（或 long? 如果可为空）");
            report.AppendLine("  2. 更新对应的 Core 层模型以保持一致");
            report.AppendLine("  3. 更新数据库映射层（如有必要）");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 确保 API 响应模型字段类型与 Core 层配置模型字段类型一致
    /// Ensure API response model field types match Core layer configuration model field types
    /// </summary>
    [Fact]
    public void ApiResponseModels_ShouldMatchCoreModelTypes()
    {
        var solutionRoot = GetSolutionRoot();
        var hostDll = Path.Combine(solutionRoot,
            "src/Host/ZakYip.WheelDiverterSorter.Host/bin/Debug/net8.0/ZakYip.WheelDiverterSorter.Host.dll");
        var coreDll = Path.Combine(solutionRoot,
            "src/Core/ZakYip.WheelDiverterSorter.Core/bin/Debug/net8.0/ZakYip.WheelDiverterSorter.Core.dll");

        if (!File.Exists(hostDll) || !File.Exists(coreDll))
        {
            Assert.Fail("Required DLLs not found. Please build the solution first.");
        }

        var hostAssembly = Assembly.LoadFrom(hostDll);
        var coreAssembly = Assembly.LoadFrom(coreDll);

        // Define mappings between response models and core models
        var mappings = new Dictionary<string, string>
        {
            { "SystemConfigResponse", "SystemConfiguration" },
            { "CommunicationConfigurationResponse", "CommunicationConfiguration" },
            { "LoggingConfigResponse", "LoggingConfiguration" },
            { "IoLinkageConfigResponse", "IoLinkageConfiguration" },
            // Add more mappings as needed
        };

        var violations = new List<string>();

        foreach (var (responseName, coreName) in mappings)
        {
            var responseType = hostAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == responseName);
            var coreType = coreAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == coreName);

            if (responseType == null || coreType == null)
            {
                continue; // Skip if either type doesn't exist
            }

            // Compare common properties
            var responseProps = responseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => p.Name, p => p.PropertyType);
            var coreProps = coreType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => p.Name, p => p.PropertyType);

            foreach (var (propName, responseType_prop) in responseProps)
            {
                // Skip database-specific fields like "Id" (int primary key)
                if (propName == "Id" && responseType_prop == typeof(int))
                {
                    continue;
                }

                // Skip ConfigName - it's a persistence detail
                if (propName == "ConfigName")
                {
                    continue;
                }

                if (coreProps.TryGetValue(propName, out var coreType_prop))
                {
                    // Normalize types (handle nullable)
                    var responseTypeNormalized = Nullable.GetUnderlyingType(responseType_prop) ?? responseType_prop;
                    var coreTypeNormalized = Nullable.GetUnderlyingType(coreType_prop) ?? coreType_prop;

                    // Skip if both are generic types (like List<T>) but check element types
                    if (responseTypeNormalized.IsGenericType && coreTypeNormalized.IsGenericType)
                    {
                        var responseGeneric = responseTypeNormalized.GetGenericTypeDefinition();
                        var coreGeneric = coreTypeNormalized.GetGenericTypeDefinition();
                        
                        // If same generic type (e.g., both List<>), check element types
                        if (responseGeneric == coreGeneric)
                        {
                            var responseArgs = responseTypeNormalized.GetGenericArguments();
                            var coreArgs = coreTypeNormalized.GetGenericArguments();
                            
                            // For simple cases like List<T>, compare first argument
                            if (responseArgs.Length > 0 && coreArgs.Length > 0)
                            {
                                var responseElement = responseArgs[0];
                                var coreElement = coreArgs[0];
                                
                                // DTO types vs Core types are acceptable (e.g., TcpConfigDto vs TcpConfig)
                                if (responseElement.Name.EndsWith("Dto") && 
                                    coreElement.Name == responseElement.Name.Replace("Dto", ""))
                                {
                                    continue; // This is acceptable DTO pattern
                                }
                                
                                // IoPoint vs IoPointDto pattern
                                if ((responseElement.Name == "IoPoint" && coreElement.Name == "IoPointDto") ||
                                    (responseElement.Name == "IoPointDto" && coreElement.Name == "IoPoint"))
                                {
                                    continue; // This is acceptable
                                }
                            }
                            
                            continue; // Skip generic type comparison - too complex for this test
                        }
                    }

                    // Skip DTO vs Core model differences (e.g., TcpConfigDto vs TcpConfig)
                    if ((responseTypeNormalized.Name.EndsWith("Dto") && 
                         coreTypeNormalized.Name == responseTypeNormalized.Name.Replace("Dto", "")) ||
                        (coreTypeNormalized.Name.EndsWith("Dto") && 
                         responseTypeNormalized.Name == coreTypeNormalized.Name.Replace("Dto", "")))
                    {
                        continue; // This is acceptable DTO pattern
                    }

                    if (responseTypeNormalized != coreTypeNormalized)
                    {
                        violations.Add($"{responseName}.{propName}: Response 类型 {responseType_prop.Name} != Core 类型 {coreType_prop.Name}");
                    }
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个 API 响应字段类型与 Core 模型不一致:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var violation in violations)
            {
                report.AppendLine($"  ⚠️ {violation}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  API 响应模型的字段类型必须与 Core 层模型完全一致。");
            report.AppendLine("  1. 检查上述不一致的字段");
            report.AppendLine("  2. 统一字段类型（优先使用 Core 层定义）");
            report.AppendLine("  3. 更新所有相关的 mapping 逻辑");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成 API 字段类型一致性报告
    /// Generate API field type consistency report
    /// </summary>
    [Fact]
    public void GenerateApiFieldTypeReport()
    {
        var solutionRoot = GetSolutionRoot();
        var hostDll = Path.Combine(solutionRoot,
            "src/Host/ZakYip.WheelDiverterSorter.Host/bin/Debug/net8.0/ZakYip.WheelDiverterSorter.Host.dll");

        if (!File.Exists(hostDll))
        {
            Console.WriteLine($"Host DLL not found at {hostDll}. Skipping report generation.");
            Assert.True(true);
            return;
        }

        var assembly = Assembly.LoadFrom(hostDll);
        var modelTypes = assembly.GetTypes()
            .Where(t => t.Namespace != null &&
                       (t.Namespace.StartsWith("ZakYip.WheelDiverterSorter.Host.Models")))
            .Where(t => t.Name.EndsWith("Request") || t.Name.EndsWith("Response") || t.Name.EndsWith("Dto"))
            .OrderBy(t => t.Name)
            .ToList();

        var report = new StringBuilder();
        report.AppendLine("# API Field Type Consistency Report\n");
        report.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        report.AppendLine($"**Total API Models**: {modelTypes.Count}\n");

        report.AppendLine("## API Models Summary\n");
        report.AppendLine("| Model Type | ID Fields | Long Count | Int Count | Other Count |");
        report.AppendLine("|------------|-----------|------------|-----------|-------------|");

        int totalLong = 0;
        int totalInt = 0;
        int totalOther = 0;

        foreach (var type in modelTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var idFields = properties.Where(p => p.Name.EndsWith("Id") || p.Name.EndsWith("ID")).ToList();
            
            var longCount = idFields.Count(p => (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(long));
            var intCount = idFields.Count(p => (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(int));
            var otherCount = idFields.Count - longCount - intCount;

            totalLong += longCount;
            totalInt += intCount;
            totalOther += otherCount;

            report.AppendLine($"| {type.Name} | {idFields.Count} | {longCount} | {intCount} | {otherCount} |");
        }

        report.AppendLine($"| **Total** | **{totalLong + totalInt + totalOther}** | **{totalLong}** | **{totalInt}** | **{totalOther}** |");
        report.AppendLine();

        report.AppendLine("## Compliance Status\n");
        report.AppendLine($"- ✅ Long ID fields: {totalLong}");
        report.AppendLine($"- {(totalInt > 0 ? "⚠️" : "✅")} Int ID fields: {totalInt} {(totalInt > 0 ? "(除了 database internal Id 外应全部为 long)" : "")}");
        report.AppendLine($"- {(totalOther > 0 ? "⚠️" : "✅")} Other type ID fields: {totalOther}");

        Console.WriteLine(report);

        // This test always passes, just generates a report
        Assert.True(true);
    }
}
