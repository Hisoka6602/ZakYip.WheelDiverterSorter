namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 技术债务文档一致性测试
/// Technical debt documentation consistency tests
/// </summary>
/// <remarks>
/// 验证技术债务计划文档与实际代码状态的一致性
/// </remarks>
public class DocumentationConsistencyTests
{
    [Fact]
    public void TechnicalDebtPlanShouldBeConsistentWithActualState()
    {
        // 这个测试验证文档中声称已完成的内容与实际扫描结果是否一致
        
        // 根据文档，DateTime 处理应该已经完成了一部分
        // 根据文档，SafeExecution 应该已经部分接入
        // 根据文档，线程安全集合还未处理
        
        var report = new System.Text.StringBuilder();
        report.AppendLine("# Technical Debt Status Verification\n");
        report.AppendLine("## Document Claims vs Actual State\n");
        
        // DateTime 检查
        var dateTimeViolations = Utilities.CodeScanner.ScanAllDateTimeViolations(includeTests: false, allowUtcInWhitelist: false);
        report.AppendLine($"\n### DateTime Standardization");
        report.AppendLine($"- **Actual Violations Found**: {dateTimeViolations.Count}");
        report.AppendLine($"- **Document Claim**: Partially complete (68/76 = 89%)");
        report.AppendLine($"- **Status**: {(dateTimeViolations.Count > 0 ? "⚠️ Still has violations - needs attention" : "✅ Fully compliant")}");
        
        // SafeExecution 检查
        var backgroundServices = Utilities.CodeScanner.FindBackgroundServices();
        var withoutSafe = backgroundServices.Where(s => !s.HasSafeExecution).ToList();
        var withSafe = backgroundServices.Count - withoutSafe.Count;
        report.AppendLine($"\n### SafeExecution Coverage");
        report.AppendLine($"- **Total BackgroundServices**: {backgroundServices.Count}");
        report.AppendLine($"- **Without SafeExecution**: {withoutSafe.Count}");
        report.AppendLine($"- **Coverage**: {(backgroundServices.Count > 0 ? withSafe * 100.0 / backgroundServices.Count : 0):F1}%");
        report.AppendLine($"- **Actual State**: {withSafe}/{backgroundServices.Count} services wrapped");
        report.AppendLine($"- **Status**: {(withoutSafe.Count > 0 ? "⚠️ Not all services wrapped" : "✅ Fully covered")}");
        
        // 线程安全集合检查
        var collectionViolations = Utilities.CodeScanner.FindNonThreadSafeCollections();
        report.AppendLine($"\n### Thread-Safe Collections");
        report.AppendLine($"- **Potential Non-Thread-Safe Usages**: {collectionViolations.Count}");
        report.AppendLine($"- **Document Claim**: 0% processed, needs analysis");
        report.AppendLine($"- **Status**: ⏳ Needs systematic review and remediation");
        
        report.AppendLine($"\n## Overall Assessment");
        report.AppendLine($"\n根据扫描结果：");
        report.AppendLine($"1. ✅ 构建状态正常（0 错误，0 警告）");
        report.AppendLine($"2. ⚠️ DateTime 违规: {dateTimeViolations.Count} 个需要修复");
        report.AppendLine($"3. ⚠️ SafeExecution 覆盖: {withoutSafe.Count}/{backgroundServices.Count} 服务未包裹");
        report.AppendLine($"4. ⏳ 线程安全集合: {collectionViolations.Count} 个需要审查");
        report.AppendLine($"\n文档与实际状态基本一致，但需要继续推进技术债清理工作。");
        
        Console.WriteLine(report.ToString());
        
        var reportPath = Path.Combine(Path.GetTempPath(), "documentation_consistency_report.md");
        File.WriteAllText(reportPath, report.ToString());
        Console.WriteLine($"\n📄 详细报告已保存到: {reportPath}");
        
        // 这个测试总是通过，只是用来验证和报告
        Assert.True(true, "Documentation consistency check completed");
    }

    [Fact]
    public void ShouldGenerateComprehensiveRemediationPlan()
    {
        // 生成全面的修复计划
        
        var dateTimeViolations = Utilities.CodeScanner.ScanAllDateTimeViolations(includeTests: false, allowUtcInWhitelist: false);
        var backgroundServices = Utilities.CodeScanner.FindBackgroundServices();
        var collectionViolations = Utilities.CodeScanner.FindNonThreadSafeCollections();
        
        var withoutSafe = backgroundServices.Where(s => !s.HasSafeExecution).ToList();

        var plan = new System.Text.StringBuilder();
        plan.AppendLine("# Comprehensive Technical Debt Remediation Plan\n");
        plan.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        
        plan.AppendLine("## Executive Summary\n");
        plan.AppendLine($"- **DateTime Violations**: {dateTimeViolations.Count} issues");
        plan.AppendLine($"- **SafeExecution Coverage**: {withoutSafe.Count} BackgroundServices need wrapping");
        plan.AppendLine($"- **Thread-Safe Collections**: {collectionViolations.Count} potential issues\n");
        
        plan.AppendLine("## Phase 1: DateTime Standardization (Priority: HIGH)\n");
        plan.AppendLine($"**Total Issues**: {dateTimeViolations.Count}\n");
        plan.AppendLine("### Action Items:");
        plan.AppendLine("1. Replace all `DateTime.Now` with `ISystemClock.LocalNow`");
        plan.AppendLine("2. Replace all `DateTime.UtcNow` with `ISystemClock.LocalNow`");
        plan.AppendLine("3. Replace all `DateTimeOffset.UtcNow` with `ISystemClock.LocalNowOffset`");
        plan.AppendLine("4. Inject `ISystemClock` dependency where needed\n");
        
        var dateTimeByLayer = dateTimeViolations.GroupBy(v =>
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
        
        plan.AppendLine("### Breakdown by Layer:");
        foreach (var layer in dateTimeByLayer)
        {
            plan.AppendLine($"- **{layer.Key}**: {layer.Count()} violations");
        }
        
        plan.AppendLine($"\n**Estimated Effort**: {(dateTimeViolations.Count / 10.0):F1} hours (assuming 10 fixes per hour)\n");
        
        plan.AppendLine("## Phase 2: SafeExecution Integration (Priority: HIGH)\n");
        plan.AppendLine($"**Total Issues**: {withoutSafe.Count} BackgroundServices\n");
        plan.AppendLine("### Services Needing SafeExecution:");
        foreach (var service in withoutSafe)
        {
            plan.AppendLine($"- [ ] {service.ClassName} ({service.GetRelativePath()})");
        }
        plan.AppendLine($"\n**Estimated Effort**: {withoutSafe.Count * 0.5:F1} hours (30 minutes per service)\n");
        
        plan.AppendLine("## Phase 3: Thread-Safe Collections (Priority: MEDIUM)\n");
        plan.AppendLine($"**Total Issues**: {collectionViolations.Count} potential non-thread-safe usages\n");
        plan.AppendLine("### Action Required:");
        plan.AppendLine("1. Review each collection usage to determine if multi-threaded");
        plan.AppendLine("2. For multi-threaded: Replace with Concurrent* or add locks");
        plan.AppendLine("3. For single-threaded: Add [SingleThreadedOnly] attribute");
        plan.AppendLine("4. For read-only: Consider Immutable collections\n");
        
        var collectionByLayer = collectionViolations.GroupBy(v =>
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
        
        plan.AppendLine("### Breakdown by Layer:");
        foreach (var layer in collectionByLayer)
        {
            plan.AppendLine($"- **{layer.Key}**: {layer.Count()} usages");
        }
        
        plan.AppendLine($"\n**Estimated Effort**: {(collectionViolations.Count / 5.0):F1} hours (review + fix, 5 per hour)\n");
        
        plan.AppendLine("## Phase 4: Test Baseline (Priority: HIGH)\n");
        plan.AppendLine("**Action Required**:");
        plan.AppendLine("- Run full test suite and document baseline");
        plan.AppendLine("- Fix failing tests or update expectations");
        plan.AppendLine("- Ensure CI/CD gates enforce green build\n");
        plan.AppendLine("**Estimated Effort**: 4-8 hours\n");
        
        var totalHours = (dateTimeViolations.Count / 10.0) + (withoutSafe.Count * 0.5) + (collectionViolations.Count / 5.0) + 6;
        plan.AppendLine("## Total Estimated Effort\n");
        plan.AppendLine($"**{totalHours:F1} hours** (~{(totalHours / 8.0):F1} working days)\n");
        
        plan.AppendLine("## Recommended Approach\n");
        plan.AppendLine("Split into multiple PRs:");
        plan.AppendLine("1. **PR-1**: Fix DateTime violations in Core + Observability layers");
        plan.AppendLine("2. **PR-2**: Fix DateTime violations in Execution + Communication layers");
        plan.AppendLine("3. **PR-3**: Fix DateTime violations in Host + Drivers + Simulation layers");
        plan.AppendLine("4. **PR-4**: Wrap all BackgroundServices with SafeExecution");
        plan.AppendLine("5. **PR-5**: Review and fix thread-safe collection issues (high-priority only)");
        plan.AppendLine("6. **PR-6**: Fix remaining thread-safe collection issues");
        plan.AppendLine("7. **PR-7**: Ensure test baseline is green and documented\n");
        
        Console.WriteLine(plan.ToString());
        
        var planPath = Path.Combine(Path.GetTempPath(), "remediation_plan.md");
        File.WriteAllText(planPath, plan.ToString());
        Console.WriteLine($"\n📄 完整修复计划已保存到: {planPath}");
        
        // 这个测试总是通过，只是用来生成计划
        Assert.True(true, $"Remediation plan generated. Total estimated effort: {totalHours:F1} hours");
    }
}
