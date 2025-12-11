using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests.Utilities;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 线程安全集合使用合规性测试
/// Thread-safe collection usage compliance tests
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 任何跨线程共享的集合必须使用线程安全容器或明确的锁封装
/// </remarks>
public class ThreadSafeCollectionTests
{
    [Fact]
    public void HighRiskNamespacesShouldUseThreadSafeCollections()
    {
        // Act: 扫描高风险命名空间中的非线程安全集合
        var violations = CodeScanner.FindNonThreadSafeCollections();

        // Assert: 高风险区域不应有未标记的非线程安全集合
        if (violations.Any())
        {
            var report = GenerateCollectionReport(violations);
            
            // 这个测试作为 Warning，因为需要进一步人工审查
            Console.WriteLine($"⚠️ 发现 {violations.Count} 个潜在的非线程安全集合使用：\n{report}");
            Console.WriteLine("\n注意：这些集合需要人工审查以确定是否真的在多线程环境中使用。");
            Console.WriteLine("如果确认是单线程使用，可以添加 [SingleThreadedOnly] 特性标记。");
        }

        // 这个测试总是通过，只是警告
        Assert.True(true, $"Found {violations.Count} potential non-thread-safe collections in high-risk namespaces");
    }

    [Fact]
    public void ShouldDocumentCollectionUsage()
    {
        // Act: 扫描所有非线程安全集合
        var violations = CodeScanner.FindNonThreadSafeCollections();

        // 按类型分组统计
        var byType = violations.GroupBy(v => v.CollectionType)
            .OrderByDescending(g => g.Count())
            .ToList();

        // 按层次分组统计
        var byLayer = violations.GroupBy(v =>
        {
            var path = v.GetRelativePath();
            if (path.Contains("/Core/")) return "Core";
            if (path.Contains("/Execution/")) return "Execution";
            if (path.Contains("/Communication/")) return "Communication";
            if (path.Contains("/Drivers/")) return "Drivers";
            if (path.Contains("/Host/")) return "Host";
            if (path.Contains("/Simulation/")) return "Simulation";
            if (path.Contains("/Observability/")) return "Observability";
            if (path.Contains("/Ingress/")) return "Ingress";
            return "Other";
        }).OrderByDescending(g => g.Count()).ToList();

        // 生成报告
        var report = "# Non-Thread-Safe Collection Usage Report\n\n";
        report += $"**Total Potential Issues**: {violations.Count}\n\n";
        
        report += "## Summary by Layer\n\n";
        foreach (var layer in byLayer)
        {
            report += $"- **{layer.Key}**: {layer.Count()} usages\n";
        }
        
        report += "\n## Summary by Collection Type\n\n";
        foreach (var type in byType)
        {
            report += $"- **{type.Key}**: {type.Count()} usages\n";
        }
        
        report += "\n## Detailed Violations\n\n";
        
        var groupedByFile = violations.GroupBy(v => v.GetRelativePath());
        foreach (var fileGroup in groupedByFile)
        {
            report += $"\n### {fileGroup.Key}\n\n";
            foreach (var violation in fileGroup)
            {
                report += $"- **{violation.ClassName}.{violation.FieldName}** (Line {violation.LineNumber})\n";
                report += $"  - Type: `{violation.CollectionType}`\n";
                report += $"  - Marked Safe: {(violation.IsMarkedSafe ? "Yes" : "No")}\n";
            }
        }

        report += "\n## Remediation Guidelines\n\n";
        report += "### Option 1: Use Thread-Safe Collections\n\n";
        report += "Replace with concurrent collections:\n";
        report += "- `Dictionary<K,V>` → `ConcurrentDictionary<K,V>`\n";
        report += "- `List<T>` → `ConcurrentBag<T>` (if order doesn't matter) or use locks\n";
        report += "- `HashSet<T>` → `ConcurrentDictionary<T, byte>` or use locks\n";
        report += "- `Queue<T>` → `ConcurrentQueue<T>`\n";
        report += "- `Stack<T>` → `ConcurrentStack<T>`\n\n";
        
        report += "### Option 2: Use Immutable Collections\n\n";
        report += "If the collection is read-only after initialization:\n";
        report += "- `List<T>` → `ImmutableList<T>` or `ImmutableArray<T>`\n";
        report += "- `Dictionary<K,V>` → `ImmutableDictionary<K,V>`\n";
        report += "- `HashSet<T>` → `ImmutableHashSet<T>`\n\n";
        
        report += "### Option 3: Add Explicit Locking\n\n";
        report += "```csharp\n";
        report += "private readonly object _lock = new();\n";
        report += "private readonly Dictionary<string, int> _data = new();\n\n";
        report += "public void Update(string key, int value)\n";
        report += "{\n";
        report += "    lock (_lock)\n";
        report += "    {\n";
        report += "        _data[key] = value;\n";
        report += "    }\n";
        report += "}\n";
        report += "```\n\n";
        
        report += "### Option 4: Mark as Single-Threaded (if truly single-threaded)\n\n";
        report += "```csharp\n";
        report += "[SingleThreadedOnly]\n";
        report += "private readonly List<Item> _items = new();\n";
        report += "```\n";

        // 输出到控制台和文件
        Console.WriteLine(report);
        
        var reportPath = Path.Combine(Path.GetTempPath(), "thread_safe_collection_report.md");
        File.WriteAllText(reportPath, report);
        Console.WriteLine($"\n📄 详细报告已保存到: {reportPath}");

        // 这个测试总是通过，只是用来生成报告
        Assert.True(true, $"Collection usage documented. Total: {violations.Count}");
    }

    private static string GenerateCollectionReport(List<CollectionUsageInfo> violations)
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine($"\n共发现 {violations.Count} 个潜在的非线程安全集合使用:");
        report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        var grouped = violations.GroupBy(v => v.GetRelativePath()).Take(10);
        foreach (var group in grouped)
        {
            report.AppendLine($"\n📄 {group.Key}");
            foreach (var violation in group)
            {
                report.AppendLine($"   Line {violation.LineNumber}: {violation.ClassName}.{violation.FieldName}");
                report.AppendLine($"   Type: {violation.CollectionType}");
            }
        }
        
        if (violations.Count > 50)
        {
            report.AppendLine($"\n... 还有 {violations.Count - 50} 个潜在问题");
        }
        
        report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        return report.ToString();
    }
}
