using ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests.Utilities;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// DateTime 使用规范合规性测试
/// DateTime usage compliance tests
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. 业务代码必须使用 ISystemClock.LocalNow 获取时间
/// 2. 禁止直接使用 DateTime.Now / DateTime.UtcNow / DateTimeOffset.Now
/// 3. 仅 SystemClock 实现类可以直接使用 DateTime.Now/UtcNow
/// </remarks>
public class DateTimeUsageComplianceTests
{
    [Fact]
    public void ShouldNotUseDirectDateTimeNowInSourceCode()
    {
        // Act: 扫描所有源代码文件
        var violations = CodeScanner.ScanAllDateTimeViolations(includeTests: false, allowUtcInWhitelist: false);
        
        // 过滤掉 ISystemClock.UtcNow 的 warnings（这个可以在特定场景使用）
        var errors = violations.Where(v => v.Severity == ViolationSeverity.Error).ToList();

        // Assert: 不应有直接的 DateTime.Now/UtcNow 或 DateTimeOffset.Now 使用
        if (errors.Any())
        {
            var report = GenerateViolationReport(errors);
            Assert.Fail($"发现 {errors.Count} 个 DateTime 使用违规：\n{report}");
        }
    }

    [Fact]
    public void ShouldNotUseUtcTimeInBusinessLogic()
    {
        // Act: 扫描所有源代码文件，包括 _clock.UtcNow 的使用
        var violations = CodeScanner.ScanAllDateTimeViolations(includeTests: false, allowUtcInWhitelist: false);
        
        // Assert: 报告所有违规（包括 warnings）
        if (violations.Any())
        {
            var report = GenerateViolationReport(violations);
            
            // 根据新需求，整个项目任何地方都不能使用UTC时间
            Assert.Fail($"发现 {violations.Count} 个 UTC 时间使用违规：\n{report}\n\n" +
                       $"⚠️ 根据最新规范，整个项目任何地方都不能使用UTC时间。\n" +
                       $"所有业务时间必须使用 ISystemClock.LocalNow。");
        }
    }

    [Fact]
    public void ShouldDocumentDateTimeViolationsForRemediation()
    {
        // Act: 扫描并生成详细报告
        var violations = CodeScanner.ScanAllDateTimeViolations(includeTests: false, allowUtcInWhitelist: false);
        
        // 按文件分组统计
        var byFile = violations.GroupBy(v => v.GetRelativePath())
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
        var report = "# DateTime Usage Violations Report\n\n";
        report += $"**Total Violations**: {violations.Count}\n\n";
        
        report += "## Summary by Layer\n\n";
        foreach (var layer in byLayer)
        {
            report += $"- **{layer.Key}**: {layer.Count()} violations\n";
        }
        
        report += "\n## Top 20 Files with Most Violations\n\n";
        foreach (var fileGroup in byFile.Take(20))
        {
            report += $"- `{fileGroup.Key}`: {fileGroup.Count()} violations\n";
        }
        
        report += "\n## Detailed Violations\n\n";
        foreach (var fileGroup in byFile)
        {
            report += $"\n### {fileGroup.Key}\n\n";
            foreach (var violation in fileGroup)
            {
                report += $"- Line {violation.LineNumber}: `{violation.Usage}` - {violation.Severity}\n";
                report += $"  ```csharp\n  {violation.CodeSnippet}\n  ```\n";
            }
        }

        // 输出到控制台和文件
        Console.WriteLine(report);
        
        var reportPath = Path.Combine(Path.GetTempPath(), "datetime_violations_report.md");
        File.WriteAllText(reportPath, report);
        Console.WriteLine($"\n📄 详细报告已保存到: {reportPath}");

        // 这个测试总是通过，只是用来生成报告
        Assert.True(true, $"DateTime violations documented. Total: {violations.Count}");
    }

    private static string GenerateViolationReport(List<DateTimeUsageViolation> violations)
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine($"\n共发现 {violations.Count} 个违规:");
        report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        var grouped = violations.GroupBy(v => v.GetRelativePath());
        foreach (var group in grouped)
        {
            report.AppendLine($"\n📄 {group.Key}");
            foreach (var violation in group.Take(5)) // 每个文件最多显示5个
            {
                report.AppendLine($"   Line {violation.LineNumber}: {violation.Usage} ({violation.Severity})");
                report.AppendLine($"   {violation.CodeSnippet}");
            }
            if (group.Count() > 5)
            {
                report.AppendLine($"   ... 还有 {group.Count() - 5} 个违规");
            }
        }
        
        report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.AppendLine("\n💡 修复建议:");
        report.AppendLine("1. 将 DateTime.Now → ISystemClock.LocalNow");
        report.AppendLine("2. 将 DateTime.UtcNow → ISystemClock.LocalNow");
        report.AppendLine("3. 将 DateTimeOffset.Now → ISystemClock.LocalNowOffset");
        report.AppendLine("4. 在构造函数注入 ISystemClock 依赖");
        
        return report.ToString();
    }
}
