using ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests.Utilities;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// SafeExecution 覆盖率合规性测试
/// SafeExecution coverage compliance tests
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 所有 BackgroundService 的 ExecuteAsync 必须通过 ISafeExecutionService 包裹
/// </remarks>
public class SafeExecutionCoverageTests
{
    [Fact]
    public void AllBackgroundServicesShouldUseSafeExecution()
    {
        // Act: 查找所有 BackgroundService 实现
        var services = CodeScanner.FindBackgroundServices();
        
        // 找出未使用 SafeExecution 的服务
        var violations = services.Where(s => !s.HasSafeExecution).ToList();

        // Assert: 所有 BackgroundService 都应使用 SafeExecution
        if (violations.Any())
        {
            var report = GenerateBackgroundServiceReport(services, violations);
            Assert.Fail($"发现 {violations.Count} 个 BackgroundService 未使用 SafeExecution：\n{report}");
        }
    }

    [Fact]
    public void ShouldDocumentBackgroundServiceCoverage()
    {
        // Act: 查找所有 BackgroundService 实现
        var services = CodeScanner.FindBackgroundServices();
        
        var withSafe = services.Where(s => s.HasSafeExecution).ToList();
        var withoutSafe = services.Where(s => !s.HasSafeExecution).ToList();

        // 生成报告
        var report = "# BackgroundService SafeExecution Coverage Report\n\n";
        report += $"**Total BackgroundServices**: {services.Count}\n";
        report += $"**With SafeExecution**: {withSafe.Count} ({(services.Count > 0 ? withSafe.Count * 100.0 / services.Count : 0):F1}%)\n";
        report += $"**Without SafeExecution**: {withoutSafe.Count} ({(services.Count > 0 ? withoutSafe.Count * 100.0 / services.Count : 0):F1}%)\n\n";

        report += "## Services WITH SafeExecution ✅\n\n";
        foreach (var service in withSafe)
        {
            report += $"- ✅ `{service.ClassName}` - {service.GetRelativePath()}\n";
        }

        report += "\n## Services WITHOUT SafeExecution ⚠️\n\n";
        foreach (var service in withoutSafe)
        {
            report += $"- ⚠️ `{service.ClassName}` - {service.GetRelativePath()}\n";
        }

        report += "\n## Remediation Steps\n\n";
        report += "For each BackgroundService without SafeExecution:\n\n";
        report += "1. Inject `ISafeExecutionService` in constructor:\n";
        report += "   ```csharp\n";
        report += "   private readonly ISafeExecutionService _safeExecutor;\n";
        report += "   \n";
        report += "   public MyWorker(ISafeExecutionService safeExecutor)\n";
        report += "   {\n";
        report += "       _safeExecutor = safeExecutor;\n";
        report += "   }\n";
        report += "   ```\n\n";
        report += "2. Wrap ExecuteAsync with SafeExecution:\n";
        report += "   ```csharp\n";
        report += "   protected override async Task ExecuteAsync(CancellationToken stoppingToken)\n";
        report += "   {\n";
        report += "       await _safeExecutor.ExecuteAsync(\n";
        report += "           async () =>\n";
        report += "           {\n";
        report += "               while (!stoppingToken.IsCancellationRequested)\n";
        report += "               {\n";
        report += "                   // Your business logic\n";
        report += "               }\n";
        report += "           },\n";
        report += "           operationName: \"MyWorkerLoop\",\n";
        report += "           cancellationToken: stoppingToken\n";
        report += "       );\n";
        report += "   }\n";
        report += "   ```\n";

        // 输出到控制台和文件
        Console.WriteLine(report);
        
        var reportPath = Path.Combine(Path.GetTempPath(), "background_service_coverage_report.md");
        File.WriteAllText(reportPath, report);
        Console.WriteLine($"\n📄 详细报告已保存到: {reportPath}");

        // 这个测试总是通过，只是用来生成报告
        Assert.True(true, $"BackgroundService coverage documented. Total: {services.Count}, Safe: {withSafe.Count}, Unsafe: {withoutSafe.Count}");
    }

    private static string GenerateBackgroundServiceReport(List<BackgroundServiceInfo> all, List<BackgroundServiceInfo> violations)
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine($"\n共发现 {violations.Count}/{all.Count} 个 BackgroundService 未使用 SafeExecution:");
        report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        foreach (var violation in violations)
        {
            report.AppendLine($"\n⚠️  {violation.ClassName}");
            report.AppendLine($"   📄 {violation.GetRelativePath()}");
        }
        
        report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.AppendLine("\n✅ 已使用 SafeExecution 的服务:");
        
        var withSafe = all.Where(s => s.HasSafeExecution).ToList();
        foreach (var service in withSafe)
        {
            report.AppendLine($"   ✅ {service.ClassName}");
        }
        
        return report.ToString();
    }
}
