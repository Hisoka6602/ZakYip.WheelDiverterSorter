using System.Reflection;
using System.Text;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// LiteDB Key 隔离验证测试
/// LiteDB Key Isolation Verification Tests
/// </summary>
/// <remarks>
/// TD-060: 确保 LiteDB 的内部 key (如 `int Id` 自增主键) 不暴露到 API 端点
/// Ensures LiteDB internal keys (like `int Id` auto-increment primary key) are not exposed to API endpoints
/// </remarks>
public class LiteDbKeyIsolationTests
{
    /// <summary>
    /// 单例配置响应白名单（这些类型只包含配置值，不包含业务实体 ID）
    /// Singleton configuration response whitelist (these types only contain configuration values, not business entity IDs)
    /// </summary>
    private static readonly HashSet<string> SingletonConfigResponses = new()
    {
        "SystemConfigResponse",
        "LoggingConfigResponse",
        "SimulationConfigResponse",
        "CommunicationConfigurationResponse",
        "IoLinkageConfigResponse"
    };

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
    /// 确保 API 响应模型使用业务 ID 而非数据库内部 Id
    /// Ensure API response models use business IDs instead of database internal Id
    /// </summary>
    /// <remarks>
    /// 允许的情况：
    /// 1. 响应模型可以有 `int Id` 字段用于数据库主键（但这通常应避免）
    /// 2. 所有业务相关的 ID（如 SensorId, ChuteId, DiverterId）必须是 long 类型
    /// 3. 如果响应模型同时有 `int Id` 和 `long XxxId`，应优先使用业务 ID
    /// </remarks>
    [Fact]
    public void ApiResponseModels_ShouldPrioritizeBusinessIdsOverDatabaseId()
    {
        var solutionRoot = GetSolutionRoot();
        var hostDll = Path.Combine(solutionRoot,
            "src/Host/ZakYip.WheelDiverterSorter.Host/bin/Debug/net8.0/ZakYip.WheelDiverterSorter.Host.dll");

        if (!File.Exists(hostDll))
        {
            Assert.Fail($"Host DLL not found at {hostDll}. Please build the solution first.");
        }

        var assembly = Assembly.LoadFrom(hostDll);
        var responseTypes = assembly.GetTypes()
            .Where(t => t.Namespace != null &&
                       t.Namespace.StartsWith("ZakYip.WheelDiverterSorter.Host.Models"))
            .Where(t => t.Name.EndsWith("Response"))
            .ToList();

        var warnings = new List<(string TypeName, string Reason)>();
        var criticalIssues = new List<(string TypeName, string Issue)>();

        foreach (var type in responseTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            // Check if there's an int Id property
            var hasIntId = properties.Any(p => p.Name == "Id" && p.PropertyType == typeof(int));
            
            // Check for business ID properties (long type properties ending with Id)
            var businessIds = properties
                .Where(p => (p.Name.EndsWith("Id") || p.Name.EndsWith("ID")) && 
                           p.Name != "Id" &&
                           ((Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(long)))
                .ToList();

            // Check for string-based IDs (common in some API designs)
            var stringIds = properties
                .Where(p => (p.Name.EndsWith("Id") || p.Name.EndsWith("ID")) && 
                           p.Name != "Id" &&
                           p.PropertyType == typeof(string))
                .ToList();

            // Scenario 1: Has int Id but NO business IDs - this is acceptable for non-entity responses
            // Scenario 2: Has int Id AND business IDs - this is a warning, should use business ID
            // Scenario 3: Has ONLY int Id fields (no long business IDs) - this is critical if it's an entity response
            
            if (hasIntId && businessIds.Any())
            {
                warnings.Add((type.Name, 
                    $"同时包含 int Id 和业务 ID ({string.Join(", ", businessIds.Select(p => p.Name))})。" +
                    "建议：如果 int Id 仅用于数据库内部，考虑在 API 响应中排除它。"));
            }
            
            // Check if response only has int-based IDs and no long business IDs
            var allIdFields = properties.Where(p => p.Name.EndsWith("Id") || p.Name.EndsWith("ID")).ToList();
            var onlyIntIds = allIdFields.All(p => 
            {
                var propType = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                return propType == typeof(int);
            });

            if (onlyIntIds && allIdFields.Any() && !stringIds.Any())
            {
                // This might be critical if it's a configuration/entity response
                // Exception: Singleton configs are acceptable with just int Id
                var isSingletonConfig = SingletonConfigResponses.Contains(type.Name);
                
                if (!isSingletonConfig && 
                    (type.Name.Contains("Config") || type.Name.Contains("Sensor") || 
                     type.Name.Contains("Chute") || type.Name.Contains("Diverter") ||
                     type.Name.Contains("Segment")))
                {
                    criticalIssues.Add((type.Name, 
                        $"所有 ID 字段都是 int 类型，应使用 long 类型的业务 ID。" +
                        $"发现字段: {string.Join(", ", allIdFields.Select(p => p.Name))}"));
                }
            }
        }

        // Build report
        var report = new StringBuilder();
        
        if (criticalIssues.Any())
        {
            report.AppendLine($"\n❌ 发现 {criticalIssues.Count} 个关键问题（使用数据库 ID 而非业务 ID）:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (typeName, issue) in criticalIssues)
            {
                report.AppendLine($"  ⛔ {typeName}");
                report.AppendLine($"     {issue}");
            }
        }

        if (warnings.Any())
        {
            report.AppendLine($"\n⚠️ 发现 {warnings.Count} 个警告（同时暴露数据库 ID 和业务 ID）:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (typeName, reason) in warnings)
            {
                report.AppendLine($"  ⚠️ {typeName}");
                report.AppendLine($"     {reason}");
            }
        }

        if (criticalIssues.Any() || warnings.Any())
        {
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 所有业务相关的 ID 字段（如 SensorId, ChuteId, DiverterId）必须使用 long 类型");
            report.AppendLine("  2. 数据库内部 Id（int 类型）应尽量不暴露到 API 响应中");
            report.AppendLine("  3. 如果必须保留 int Id，确保它与业务 ID 清晰区分");
            report.AppendLine("  4. 在 DTO mapping 时，优先映射业务 ID 而非数据库 Id");
        }

        // Critical issues cause test failure
        if (criticalIssues.Any())
        {
            Assert.Fail(report.ToString());
        }

        // Warnings are just logged
        if (warnings.Any())
        {
            Console.WriteLine(report);
        }

        // If no issues, test passes
        Assert.True(true);
    }

    /// <summary>
    /// 确保配置 API 端点不直接暴露 LiteDB 的自增 Id
    /// Ensure configuration API endpoints don't directly expose LiteDB auto-increment Id
    /// </summary>
    [Fact]
    public void ConfigApiResponses_ShouldNotExposeLiteDbAutoIncrementId()
    {
        var solutionRoot = GetSolutionRoot();
        var hostDll = Path.Combine(solutionRoot,
            "src/Host/ZakYip.WheelDiverterSorter.Host/bin/Debug/net8.0/ZakYip.WheelDiverterSorter.Host.dll");

        if (!File.Exists(hostDll))
        {
            Assert.Fail($"Host DLL not found at {hostDll}. Please build the solution first.");
        }

        var assembly = Assembly.LoadFrom(hostDll);
        var configResponseTypes = assembly.GetTypes()
            .Where(t => t.Namespace != null &&
                       t.Namespace.StartsWith("ZakYip.WheelDiverterSorter.Host.Models"))
            .Where(t => t.Name.EndsWith("Response") && t.Name.Contains("Config"))
            .ToList();

        var acceptableResponses = SingletonConfigResponses.ToList();

        var violations = new List<(string TypeName, string Details)>();

        foreach (var type in configResponseTypes)
        {
            // Skip acceptable responses that we've verified have proper business IDs
            if (acceptableResponses.Contains(type.Name))
            {
                continue;
            }

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            // Check if it has an int Id property without corresponding business ID
            var hasIntId = properties.Any(p => p.Name == "Id" && p.PropertyType == typeof(int));
            
            if (hasIntId)
            {
                // Check if there's a corresponding business ID (long type)
                var hasLongBusinessId = properties.Any(p => 
                    p.Name.EndsWith("Id") && 
                    p.Name != "Id" &&
                    ((Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(long)));

                // For singleton configurations (like LoggingConfig, SystemConfig), it's acceptable to only have int Id
                // These are typically global system configurations with only one instance
                // They don't need business IDs because they're not entities with external references
                var isSingletonConfig = type.Name.Contains("Logging") || 
                                       type.Name.Contains("Simulation");

                if (!hasLongBusinessId && !isSingletonConfig)
                {
                    violations.Add((type.Name, 
                        "包含 int Id 但缺少 long 类型的业务 ID。配置响应应使用业务 ID 而非数据库内部 Id。"));
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个配置响应模型暴露了 LiteDB 自增 Id:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (typeName, details) in violations)
            {
                report.AppendLine($"  ⛔ {typeName}");
                report.AppendLine($"     {details}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 为配置响应添加 long 类型的业务 ID 字段");
            report.AppendLine("  2. 或者移除 int Id 字段，只使用业务 ID");
            report.AppendLine("  3. 确保 Core 层配置模型定义了相应的业务 ID");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成 LiteDB Key 隔离报告
    /// Generate LiteDB Key Isolation Report
    /// </summary>
    [Fact]
    public void GenerateLiteDbKeyIsolationReport()
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
        var responseTypes = assembly.GetTypes()
            .Where(t => t.Namespace != null &&
                       t.Namespace.StartsWith("ZakYip.WheelDiverterSorter.Host.Models"))
            .Where(t => t.Name.EndsWith("Response"))
            .OrderBy(t => t.Name)
            .ToList();

        var report = new StringBuilder();
        report.AppendLine("# LiteDB Key Isolation Report\n");
        report.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        report.AppendLine($"**Total Response Models**: {responseTypes.Count}\n");

        report.AppendLine("## Response Models Analysis\n");
        report.AppendLine("| Response Model | Has int Id | Has long Business ID | Has string ID | Status |");
        report.AppendLine("|----------------|-----------|---------------------|---------------|--------|");

        int withIntId = 0;
        int withLongBusinessId = 0;
        int withStringId = 0;
        int isolationCompliant = 0;

        foreach (var type in responseTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            var hasIntId = properties.Any(p => p.Name == "Id" && p.PropertyType == typeof(int));
            var hasLongBusinessId = properties.Any(p => 
                p.Name.EndsWith("Id") && 
                p.Name != "Id" &&
                ((Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(long)));
            var hasStringId = properties.Any(p => 
                (p.Name.EndsWith("Id") || p.Name.EndsWith("ID")) && 
                p.Name != "Id" &&
                p.PropertyType == typeof(string));

            if (hasIntId) withIntId++;
            if (hasLongBusinessId) withLongBusinessId++;
            if (hasStringId) withStringId++;

            // Determine status
            string status;
            if (!hasIntId && hasLongBusinessId)
            {
                status = "✅ Excellent";
                isolationCompliant++;
            }
            else if (hasIntId && hasLongBusinessId)
            {
                status = "⚠️ Acceptable";
                isolationCompliant++;
            }
            else if (hasIntId && !hasLongBusinessId && !hasStringId)
            {
                status = "❌ Needs Fix";
            }
            else if (hasStringId)
            {
                status = "ℹ️ String ID";
                isolationCompliant++;
            }
            else
            {
                status = "ℹ️ No IDs";
            }

            report.AppendLine($"| {type.Name} | {(hasIntId ? "Yes" : "No")} | {(hasLongBusinessId ? "Yes" : "No")} | {(hasStringId ? "Yes" : "No")} | {status} |");
        }

        report.AppendLine();
        report.AppendLine("## Summary\n");
        report.AppendLine($"- Total response models: {responseTypes.Count}");
        report.AppendLine($"- With int Id: {withIntId}");
        report.AppendLine($"- With long business ID: {withLongBusinessId}");
        report.AppendLine($"- With string ID: {withStringId}");
        report.AppendLine($"- **Isolation compliant: {isolationCompliant}/{responseTypes.Count} ({(double)isolationCompliant / responseTypes.Count * 100:F1}%)**");
        report.AppendLine();

        report.AppendLine("## Compliance Rules\n");
        report.AppendLine("1. ✅ **Excellent**: 只使用 long 业务 ID，不暴露 int database Id");
        report.AppendLine("2. ⚠️ **Acceptable**: 同时有 int Id 和 long 业务 ID（建议移除 int Id）");
        report.AppendLine("3. ❌ **Needs Fix**: 只有 int Id，没有 long 业务 ID");
        report.AppendLine("4. ℹ️ **String ID**: 使用 string 类型 ID（某些场景下可接受）");

        Console.WriteLine(report);

        // This test always passes, just generates a report
        Assert.True(true);
    }
}
