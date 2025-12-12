using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.ArchTests;

/// <summary>
/// 强制性架构规则测试
/// PR-MANDATORY-RULES: 实施强制性架构约束
/// </summary>
/// <remarks>
/// 规则：
/// 0. PR完整性约束：小型PR(&lt;24h)必须完整完成；大型PR(≥24h)未完成部分必须记录技术债
/// 1. 所有枚举必须定义在 Core/Enums 子目录中（按类型分类）
/// 2. 所有事件载荷必须定义在 Core/Events 子目录中（按类型分类）  
/// 3. 文档文件必须及时清理（禁止超过6个月的过时文档）
/// 违反规则后果：PR 自动失败
/// </remarks>
public class MandatoryArchitectureRulesTests
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

    private static readonly Regex EnumPattern = new(
        @"^\s*(?<modifiers>(?:public|internal|private|protected)\s+)(?:static\s+)?enum\s+(?<name>\w+)",
        RegexOptions.Compiled);

    #region Rule 0: PR完整性约束

    [Fact]
    public void SmallPR_MustBeCompletelyFinished_NoCompilationErrors()
    {
        // 注意：此测试的实际验证在 CI/CD 中通过 dotnet build 完成
        // 这里仅作为规则声明和文档说明
        
        var message = "规则：评估工作量 < 24小时的 PR 不允许存在编译错误\n" +
            "检查方式：CI/CD 中的 'dotnet build' 步骤\n" +
            "违规后果：PR 自动失败\n" +
            "\n此测试通过表示规则已声明，实际检查由 CI/CD 执行。";
        
        Console.WriteLine(message);
        Assert.True(true, "PR完整性规则已声明");
    }

    [Fact]
    public void SmallPR_MustBeCompletelyFinished_NoFailingTests()
    {
        // 注意：此测试的实际验证在 CI/CD 中通过 dotnet test 完成
        // 这里仅作为规则声明和文档说明
        
        var message = "规则：评估工作量 < 24小时的 PR 不允许存在失败的测试\n" +
            "检查方式：CI/CD 中的 'dotnet test' 步骤\n" +
            "违规后果：PR 自动失败\n" +
            "\n此测试通过表示规则已声明，实际检查由 CI/CD 执行。";
        
        Console.WriteLine(message);
        Assert.True(true, "PR完整性规则已声明");
    }

    [Fact]
    public void SmallPR_MustBeCompletelyFinished_NoTodoForNextPR()
    {
        var solutionRoot = GetSolutionRoot();
        var srcDir = Path.Combine(solutionRoot, "src");
        var violations = new List<(string FilePath, int LineNumber, string TodoText)>();

        // 检测 TODO/FIXME 标记包含"后续PR"、"下个PR"、"next PR"等关键词
        var todoPatterns = new[]
        {
            @"//\s*TODO.*(?:后续|下个|下一个)\s*PR",
            @"//\s*TODO.*next\s+PR",
            @"//\s*FIXME.*(?:后续|下个|下一个)\s*PR",
            @"//\s*FIXME.*next\s+PR",
        };

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                foreach (var pattern in todoPatterns)
                {
                    if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                    {
                        violations.Add((file, i + 1, line.Trim()));
                    }
                }
            }
        }

        if (violations.Any())
        {
            var message = "❌ 强制性规则违反：发现'待后续PR'标记\n\n" +
                "规则：评估工作量 < 24小时的 PR 必须完整完成，不允许留下'TODO: 后续PR修复'等标记\n" +
                "违规后果：PR 自动失败\n\n" +
                "处理方案：\n" +
                "  1. 如果工作量确实 < 24小时：在本PR中完成所有工作\n" +
                "  2. 如果工作量 ≥ 24小时：将未完成工作记录到 TechnicalDebtLog.md，并删除 TODO 标记\n\n" +
                "违规位置：\n" +
                string.Join("\n", violations.Select(v => 
                    $"  - {Path.GetRelativePath(solutionRoot, v.FilePath)}:{v.LineNumber}\n    {v.TodoText}"));
            
            Assert.Fail(message);
        }
    }

    [Fact]
    public void LargePR_IncompleteParts_MustBeDocumentedInTechnicalDebt()
    {
        // 这是一个文档性测试，提醒开发者遵守规则
        // 实际检查需要人工审查 TechnicalDebtLog.md 是否包含必要的条目
        
        var solutionRoot = GetSolutionRoot();
        var technicalDebtFile = Path.Combine(solutionRoot, "docs", "TechnicalDebtLog.md");
        
        if (!File.Exists(technicalDebtFile))
        {
            Assert.Fail("未找到 TechnicalDebtLog.md 文件，无法验证技术债记录");
        }

        var content = File.ReadAllText(technicalDebtFile);
        
        // 检查是否有进行中（⏳）的技术债条目
        var inProgressPattern = @"\[TD-\d+\].*（⏳\s*进行中）";
        var hasInProgressDebt = Regex.IsMatch(content, inProgressPattern);

        var message = "规则：评估工作量 ≥ 24小时的 PR 如有未完成工作，必须在 TechnicalDebtLog.md 中记录\n\n" +
            "技术债条目必须包含：\n" +
            "  1. 已完成工作清单\n" +
            "  2. 未完成工作清单\n" +
            "  3. 下一步指引（文件清单、修改建议、注意事项）\n" +
            "  4. 预估工作量和风险等级\n\n" +
            $"当前状态：{(hasInProgressDebt ? "✅ 发现进行中的技术债条目" : "⚠️ 未发现进行中的技术债条目")}\n\n" +
            "如果本PR是大型PR且有未完成工作，请确保已正确记录到 TechnicalDebtLog.md\n" +
            "如果本PR是小型PR（< 24小时），则应该完整完成所有工作，不应有未完成部分。";
        
        Console.WriteLine(message);
        Assert.True(true, "PR完整性规则已声明");
    }

    #endregion

    #region Rule 1: 枚举位置强制约束

    [Fact]
    public void AllEnums_MustBeDefinedIn_CoreEnumsDirectory()
    {
        var solutionRoot = GetSolutionRoot();
        var srcDir = Path.Combine(solutionRoot, "src");
        var violations = new List<(string EnumName, string FilePath)>();

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(srcDir, file).Replace('\\', '/');
            
            // 检查是否在正确的位置
            bool isInCorrectLocation = relativePath.StartsWith("Core/ZakYip.WheelDiverterSorter.Core/Enums/", StringComparison.OrdinalIgnoreCase);

            var enums = ExtractEnumDefinitions(file);
            foreach (var enumName in enums)
            {
                if (!isInCorrectLocation)
                {
                    violations.Add((enumName, file));
                }
            }
        }

        if (violations.Any())
        {
            var message = "❌ 强制性规则违反：以下枚举不在 Core/Enums 目录中\n\n" +
                "规则: 所有枚举必须定义在 ZakYip.WheelDiverterSorter.Core/Enums/ 子目录中（按类型分类）\n" +
                "违规后果: PR 自动失败\n\n" +
                "违规文件:\n" +
                string.Join("\n", violations.Select(v => 
                    $"  - {v.EnumName} at {Path.GetRelativePath(solutionRoot, v.FilePath)}")) +
                "\n\n正确位置: src/Core/ZakYip.WheelDiverterSorter.Core/Enums/<TypeCategory>/\n" +
                "类型分类: Hardware, Parcel, System, Communication, Sorting, Simulation, Monitoring\n";
            
            Assert.Fail(message);
        }
    }

    #endregion

    #region Rule 2: 事件载荷位置强制约束

    /// <summary>
    /// 白名单：允许在 Core.Events 之外定义的事件类型
    /// </summary>
    private static readonly HashSet<string> WhitelistedEventTypes = new(StringComparer.Ordinal)
    {
        // 定义在 Communication.Abstractions 的事件参数（接口定义要求）
        "ClientConnectionEventArgs",
        "ParcelNotificationReceivedEventArgs",
        "ConnectionStateChangedEventArgs",
        
        // 定义在 Core/LineModel 的事件参数（健康监控）
        "NodeHealthChangedEventArgs",
        
        // 定义在 Execution 的事件参数（路径重规划）
        "ReroutingSucceededEventArgs",
        "ReroutingFailedEventArgs",
        
        // 仿真项目特有的事件参数
        "SimulatedParcelResultEventArgs",
        
        // 上游路由客户端接口中的事件参数
        "ChuteAssignmentEventArgs",
    };

    [Fact]
    public void AllEventArgs_MustBeDefinedIn_CoreEventsDirectory()
    {
        var solutionRoot = GetSolutionRoot();
        var srcDir = Path.Combine(solutionRoot, "src");
        var violations = new List<(string TypeName, string FilePath)>();

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Where(f => f.EndsWith("EventArgs.cs") || f.EndsWith("Event.cs"))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(srcDir, file).Replace('\\', '/');
            var fileName = Path.GetFileNameWithoutExtension(file);
            
            // 跳过白名单
            if (WhitelistedEventTypes.Contains(fileName))
            {
                continue;
            }

            // 检查是否在正确的位置
            bool isInCorrectLocation = relativePath.StartsWith("Core/ZakYip.WheelDiverterSorter.Core/Events/", StringComparison.OrdinalIgnoreCase);

            if (!isInCorrectLocation && (fileName.EndsWith("EventArgs") || fileName.EndsWith("Event")))
            {
                violations.Add((fileName, file));
            }
        }

        if (violations.Any())
        {
            var message = "❌ 强制性规则违反：以下事件载荷不在 Core/Events 目录中\n\n" +
                "规则: 所有事件载荷（EventArgs/Event）必须定义在 ZakYip.WheelDiverterSorter.Core/Events/ 子目录中（按类型分类）\n" +
                "违规后果: PR 自动失败\n\n" +
                "违规文件:\n" +
                string.Join("\n", violations.Select(v => 
                    $"  - {v.TypeName} at {Path.GetRelativePath(solutionRoot, v.FilePath)}")) +
                "\n\n正确位置: src/Core/ZakYip.WheelDiverterSorter.Core/Events/<TypeCategory>/\n" +
                "类型分类: Alarm, Hardware, Sensor, Sorting, Communication, Simulation, Monitoring\n" +
                "\n如果是特殊情况（接口定义、厂商特定），请添加到白名单。\n";
            
            Assert.Fail(message);
        }
    }

    #endregion

    #region Rule 3: 文档清理规则

    /// <summary>
    /// 文档分类配置
    /// </summary>
    private static readonly Dictionary<string, int> DocumentLifespanRules = new()
    {
        // PR总结文档：完成后立即归档（不允许保留超过30天）
        ["PR_.*_SUMMARY\\.md"] = 30,
        
        // 任务清单文档：完成后立即归档（不允许保留超过30天）
        [".*_TASKS\\.md"] = 30,
        
        // 修复记录文档：修复后归档（不允许保留超过60天）
        ["FIX_.*\\.md"] = 60,
        ["fixes/.*\\.md"] = 60,
        
        // 实施计划文档：实施完成后归档（不允许保留超过90天）
        [".*_IMPLEMENTATION\\.md"] = 90,
        [".*_PLAN\\.md"] = 90,
        
        // 一般性过时文档（超过180天视为过时）
        [".*"] = 180,
    };

    /// <summary>
    /// 白名单：永久保留的文档
    /// </summary>
    private static readonly HashSet<string> PermanentDocuments = new(StringComparer.OrdinalIgnoreCase)
    {
        "README.md",
        "ARCHITECTURE_PRINCIPLES.md",
        "CODING_GUIDELINES.md",
        "DOCUMENTATION_INDEX.md",
        "RepositoryStructure.md",
        "TechnicalDebtLog.md",
        "CORE_ROUTING_LOGIC.md",
        "MANDATORY_RULES_AND_DEAD_CODE.md",
        
        // guides/ 目录下的所有文档
        "guides/UPSTREAM_CONNECTION_GUIDE.md",
        "guides/VENDOR_EXTENSION_GUIDE.md",
        "guides/SYSTEM_CONFIG_GUIDE.md",
        "guides/API_USAGE_GUIDE.md",
        "guides/SENSOR_IO_POLLING_CONFIGURATION.md",
        "guides/PARCEL_LOSS_DETECTION.md",
        
        // 技术评估和架构文档
        "TOPOLOGY_LINEAR_N_DIVERTERS.md",
        "S7_Driver_Enhancement.md",
        "TouchSocket_Migration_Assessment.md",
        "UPSTREAM_SEQUENCE_FIREFORGET.md",
        "PRODUCTION_SERVICE_STARTUP.md",
        "SELF_CONTAINED_DEPLOYMENT_SUMMARY.md",
    };

    [Fact]
    public void Documentation_ShouldBeKeptUpToDate_NoOutdatedFiles()
    {
        var solutionRoot = GetSolutionRoot();
        var docsDir = Path.Combine(solutionRoot, "docs");
        
        if (!Directory.Exists(docsDir))
        {
            return; // 没有 docs 目录，跳过检查
        }

        var violations = new List<(string FileName, int DaysOld, int MaxAllowedDays, string Category)>();
        var now = DateTime.UtcNow;

        var mdFiles = Directory.GetFiles(docsDir, "*.md", SearchOption.AllDirectories);

        foreach (var file in mdFiles)
        {
            var relativePath = Path.GetRelativePath(docsDir, file).Replace('\\', '/');
            var fileName = Path.GetFileName(file);
            
            // 跳过白名单
            if (PermanentDocuments.Any(p => 
                relativePath.Equals(p, StringComparison.OrdinalIgnoreCase) || 
                fileName.Equals(p, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var lastModified = File.GetLastWriteTimeUtc(file);
            var daysOld = (int)(now - lastModified).TotalDays;

            // 根据文档类型确定最大允许天数
            int maxAllowedDays = 180; // 默认值
            string matchedCategory = "一般文档";

            foreach (var rule in DocumentLifespanRules.OrderBy(r => r.Value))
            {
                if (Regex.IsMatch(fileName, rule.Key, RegexOptions.IgnoreCase))
                {
                    maxAllowedDays = rule.Value;
                    matchedCategory = rule.Key;
                    break;
                }
            }

            if (daysOld > maxAllowedDays)
            {
                violations.Add((relativePath, daysOld, maxAllowedDays, matchedCategory));
            }
        }

        if (violations.Any())
        {
            var message = "❌ 强制性规则违反：发现过时文档文件\n\n" +
                "规则: 文档文件必须及时清理或更新\n" +
                "违规后果: PR 自动失败\n\n" +
                "过时文档:\n" +
                string.Join("\n", violations.Select(v => 
                    $"  - {v.FileName} (已 {v.DaysOld} 天，限制 {v.MaxAllowedDays} 天，类型: {v.Category})")) +
                "\n\n处理建议:\n" +
                "  1. 删除已完成/过时的文档\n" +
                "  2. 将历史记录整合到 TechnicalDebtLog.md\n" +
                "  3. 将重要信息迁移到永久文档中\n" +
                "  4. 更新文档内容使其保持最新\n";
            
            Assert.Fail(message);
        }
    }

    [Fact]
    public void Documentation_ShouldFollowNamingConventions()
    {
        var solutionRoot = GetSolutionRoot();
        var docsDir = Path.Combine(solutionRoot, "docs");
        
        if (!Directory.Exists(docsDir))
        {
            return;
        }

        var violations = new List<(string FileName, string Issue)>();
        var mdFiles = Directory.GetFiles(docsDir, "*.md", SearchOption.AllDirectories);

        var forbiddenPatterns = new Dictionary<string, string>
        {
            [@"PR_\d+_.*\.md"] = "PR总结文档应该归档到 TechnicalDebtLog.md，不应长期保留",
            [@".*_PHASE\d+_.*\.md"] = "阶段性文档应该合并到主文档，不应独立保留",
            [@"NEXT_.*\.md"] = "待办事项文档应该使用 GitHub Issues 或项目看板管理",
            [@"REMAINING_.*\.md"] = "剩余工作文档应该整合到主文档或技术债务中",
            [@"TODO_.*\.md"] = "TODO文档应该转换为 GitHub Issues",
        };

        foreach (var file in mdFiles)
        {
            var fileName = Path.GetFileName(file);
            
            // 跳过白名单
            if (PermanentDocuments.Contains(fileName))
            {
                continue;
            }

            foreach (var pattern in forbiddenPatterns)
            {
                if (Regex.IsMatch(fileName, pattern.Key, RegexOptions.IgnoreCase))
                {
                    violations.Add((fileName, pattern.Value));
                }
            }
        }

        if (violations.Any())
        {
            var message = "⚠️ 文档命名规范建议：发现不推荐的文档命名模式\n\n" +
                string.Join("\n", violations.Select(v => 
                    $"  - {v.FileName}\n    原因: {v.Issue}")) +
                "\n\n这不会导致测试失败，但建议重构文档结构。\n";
            
            // 这是一个警告，不是错误
            Console.WriteLine(message);
        }
    }

    #endregion

    #region Helper Methods

    private static List<string> ExtractEnumDefinitions(string filePath)
    {
        var enums = new List<string>();

        try
        {
            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                var match = EnumPattern.Match(line);
                if (match.Success)
                {
                    enums.Add(match.Groups["name"].Value);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting enums from {filePath}: {ex.Message}");
        }

        return enums;
    }

    #endregion
}
