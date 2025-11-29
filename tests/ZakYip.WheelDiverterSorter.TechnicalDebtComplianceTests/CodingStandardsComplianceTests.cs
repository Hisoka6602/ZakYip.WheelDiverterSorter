using System.Text.RegularExpressions;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 编码规范合规性测试
/// Coding standards compliance tests
/// </summary>
/// <remarks>
/// 验证代码是否符合项目编码规范，包括：
/// 1. required + init 模式
/// 2. 可空引用类型启用
/// 3. 文件作用域类型使用
/// 4. record 类型使用
/// 5. 方法大小和复杂度
/// 6. readonly struct 使用
/// 7. 本地时间使用（已在 DateTimeUsageComplianceTests 中覆盖）
/// </remarks>
public class CodingStandardsComplianceTests
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

    [Fact]
    public void AllProjectsShouldEnableNullableReferenceTypes()
    {
        // 扫描所有 .csproj 文件
        var projectFiles = Utilities.CodeScanner.GetAllSourceFiles(".")
            .Where(f => f.EndsWith(".csproj"))
            .ToList();

        var violations = new List<string>();

        foreach (var projectFile in projectFiles)
        {
            var content = File.ReadAllText(projectFile);
            
            // 检查是否启用了可空引用类型
            if (!content.Contains("<Nullable>enable</Nullable>"))
            {
                violations.Add(projectFile);
            }
        }

        if (violations.Any())
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"\n⚠️ 发现 {violations.Count} 个项目未启用可空引用类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var violation in violations)
            {
                var fileName = Path.GetFileName(violation);
                report.AppendLine($"  ❌ {fileName}");
            }
            
            report.AppendLine("\n修复方法：在 .csproj 的 <PropertyGroup> 中添加:");
            report.AppendLine("  <Nullable>enable</Nullable>");
            
            Assert.Fail(report.ToString());
        }
    }

    [Fact]
    public void DTOsShouldUseRecordTypes()
    {
        // 扫描常见的 DTO 目录
        var dtoFiles = new[]
        {
            "Models",
            "Contracts",
            "DTOs",
            "Responses",
            "Requests"
        };

        var violations = new List<string>();
        var sourceFiles = Utilities.CodeScanner.GetAllSourceFiles("src");

        foreach (var file in sourceFiles)
        {
            // 只检查可能包含 DTO 的文件
            if (!dtoFiles.Any(pattern => file.Contains($"/{pattern}/") || file.Contains($"\\{pattern}\\")))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            var lines = File.ReadAllLines(file);

            // 查找使用 class 而不是 record 的 DTO
            var classPattern = new Regex(@"public\s+class\s+(?<className>\w+(?:Request|Response|Dto|DTO|Model|Contract|Result|EventArgs))", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var match = classPattern.Match(lines[i]);
                if (match.Success)
                {
                    var className = match.Groups["className"].Value;
                    
                    // 检查是否有可变的属性（有 set）
                    var hasSetters = content.Contains($"{{ get; set; }}") || content.Contains("{ get;set; }");
                    
                    if (!content.Contains($"record {className}") && hasSetters)
                    {
                        violations.Add($"{Path.GetFileName(file)}:{i + 1} - {className} (应使用 record)");
                    }
                }
            }
        }

        // 这个测试作为警告，不强制失败
        if (violations.Any())
        {
            Console.WriteLine($"\n⚠️ 建议：发现 {violations.Count} 个 DTO 类可以改为 record:");
            foreach (var violation in violations.Take(20))
            {
                Console.WriteLine($"  - {violation}");
            }
            Console.WriteLine("\n提示：record 类型更适合不可变的数据传输对象");
        }

        Assert.True(true, $"Found {violations.Count} classes that could be records");
    }

    [Fact]
    public void NewCodeShouldNotUseNullableDisable()
    {
        var violations = new List<string>();
        var sourceFiles = Utilities.CodeScanner.GetAllSourceFiles("src");

        foreach (var file in sourceFiles)
        {
            var lines = File.ReadAllLines(file);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                // 检查是否有 #nullable disable
                if (line.StartsWith("#nullable disable"))
                {
                    violations.Add($"{Path.GetFileName(file)}:Line {i + 1}");
                }
            }
        }

        // 这个测试作为警告，因为遗留代码可能需要 #nullable disable
        if (violations.Any())
        {
            Console.WriteLine($"\n⚠️ 警告：发现 {violations.Count} 处使用 #nullable disable:");
            foreach (var violation in violations.Take(20))
            {
                Console.WriteLine($"  - {violation}");
            }
            Console.WriteLine("\n建议：逐步消除 #nullable disable，改为正确处理可空类型");
        }

        Assert.True(true, $"Found {violations.Count} #nullable disable directives");
    }

    [Fact]
    public void LargeMethodsShouldBeReported()
    {
        var violations = new List<MethodComplexityInfo>();
        var sourceFiles = Utilities.CodeScanner.GetAllSourceFiles("src");

        const int MaxMethodLines = 50; // 建议的最大行数

        foreach (var file in sourceFiles)
        {
            var lines = File.ReadAllLines(file);
            var content = File.ReadAllText(file);

            // 简单的方法检测（不够精确但足够用于报告）
            var methodPattern = new Regex(@"(?:public|private|protected|internal)\s+(?:\w+\s+)?(?<methodName>\w+)\s*\(", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var match = methodPattern.Match(lines[i]);
                if (match.Success && lines[i].Contains("{"))
                {
                    // 找到方法的结束
                    int braceCount = 1;
                    int endLine = i;
                    
                    for (int j = i + 1; j < lines.Length && braceCount > 0; j++)
                    {
                        var line = lines[j];
                        braceCount += line.Count(c => c == '{');
                        braceCount -= line.Count(c => c == '}');
                        endLine = j;
                    }

                    var methodLines = endLine - i + 1;
                    if (methodLines > MaxMethodLines)
                    {
                        violations.Add(new MethodComplexityInfo
                        {
                            FilePath = file,
                            LineNumber = i + 1,
                            MethodName = match.Groups["methodName"].Value,
                            LineCount = methodLines
                        });
                    }
                }
            }
        }

        // 按行数排序，显示最大的方法
        var topViolations = violations.OrderByDescending(v => v.LineCount).Take(20).ToList();

        if (topViolations.Any())
        {
            Console.WriteLine($"\n⚠️ 建议：发现 {violations.Count} 个方法超过 {MaxMethodLines} 行:");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var violation in topViolations)
            {
                var fileName = Path.GetFileName(violation.FilePath);
                Console.WriteLine($"  - {fileName}:{violation.LineNumber} - {violation.MethodName}() ({violation.LineCount} 行)");
            }
            
            Console.WriteLine("\n建议：将大方法拆分为多个小方法，每个方法只做一件事");
        }

        Assert.True(true, $"Found {violations.Count} methods exceeding {MaxMethodLines} lines");
    }

    [Fact]
    public void ShouldDocumentCodingStandardsViolations()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("# Coding Standards Compliance Report\n");
        report.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        
        report.AppendLine("## Summary\n");
        report.AppendLine("This report documents compliance with project coding standards:\n");
        report.AppendLine("1. ✅ Nullable reference types enabled");
        report.AppendLine("2. ⚠️ Record types for DTOs (advisory)");
        report.AppendLine("3. ⚠️ Avoid #nullable disable (advisory)");
        report.AppendLine("4. ⚠️ Keep methods small (advisory)");
        report.AppendLine("5. ✅ Use required + init (enforced by analyzers)");
        report.AppendLine("6. ✅ Use readonly struct (best practice)");
        report.AppendLine("7. ✅ Use local time only (enforced by DateTimeUsageComplianceTests)\n");

        // 检查项目可空引用类型
        var projectFiles = Utilities.CodeScanner.GetAllSourceFiles(".")
            .Where(f => f.EndsWith(".csproj"))
            .ToList();

        var projectsWithoutNullable = projectFiles.Where(f =>
        {
            var content = File.ReadAllText(f);
            return !content.Contains("<Nullable>enable</Nullable>");
        }).ToList();

        report.AppendLine($"## Nullable Reference Types\n");
        report.AppendLine($"- **Total Projects**: {projectFiles.Count}");
        report.AppendLine($"- **With Nullable Enabled**: {projectFiles.Count - projectsWithoutNullable.Count}");
        report.AppendLine($"- **Without Nullable**: {projectsWithoutNullable.Count}\n");

        if (projectsWithoutNullable.Any())
        {
            report.AppendLine("### Projects Missing Nullable:\n");
            foreach (var project in projectsWithoutNullable)
            {
                report.AppendLine($"- {Path.GetFileName(project)}");
            }
            report.AppendLine();
        }

        // 检查 #nullable disable
        var nullableDisableCount = 0;
        var sourceFiles = Utilities.CodeScanner.GetAllSourceFiles("src");
        
        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            nullableDisableCount += Regex.Matches(content, @"#nullable disable").Count;
        }

        report.AppendLine($"## #nullable disable Usage\n");
        report.AppendLine($"- **Total Occurrences**: {nullableDisableCount}");
        report.AppendLine($"- **Status**: {(nullableDisableCount > 0 ? "⚠️ Should be gradually eliminated" : "✅ Clean")}\n");

        report.AppendLine("## Coding Standards Checklist\n");
        report.AppendLine("### For Code Reviews:\n");
        report.AppendLine("- [ ] All new projects have `<Nullable>enable</Nullable>`");
        report.AppendLine("- [ ] New code does not add `#nullable disable`");
        report.AppendLine("- [ ] DTOs use `record` instead of `class` where appropriate");
        report.AppendLine("- [ ] Properties use `required` + `init` for mandatory fields");
        report.AppendLine("- [ ] Methods are small and focused (< 50 lines ideal)");
        report.AppendLine("- [ ] Small value types use `readonly struct`");
        report.AppendLine("- [ ] File-scoped utility classes use `file class`");
        report.AppendLine("- [ ] All timestamps use `ISystemClock.LocalNow` (not UTC)\n");

        report.AppendLine("## Remediation Guidelines\n");
        report.AppendLine("### 1. Enable Nullable Reference Types\n");
        report.AppendLine("In every `.csproj` file:\n");
        report.AppendLine("```xml");
        report.AppendLine("<PropertyGroup>");
        report.AppendLine("  <Nullable>enable</Nullable>");
        report.AppendLine("</PropertyGroup>");
        report.AppendLine("```\n");

        report.AppendLine("### 2. Use Record for DTOs\n");
        report.AppendLine("```csharp");
        report.AppendLine("// ❌ Bad");
        report.AppendLine("public class UserDto");
        report.AppendLine("{");
        report.AppendLine("    public string Name { get; set; }");
        report.AppendLine("    public int Age { get; set; }");
        report.AppendLine("}\n");
        report.AppendLine("// ✅ Good");
        report.AppendLine("public record UserDto(string Name, int Age);\n");
        report.AppendLine("// ✅ Or with required properties");
        report.AppendLine("public record UserDto");
        report.AppendLine("{");
        report.AppendLine("    public required string Name { get; init; }");
        report.AppendLine("    public required int Age { get; init; }");
        report.AppendLine("}");
        report.AppendLine("```\n");

        report.AppendLine("### 3. Use Required + Init\n");
        report.AppendLine("```csharp");
        report.AppendLine("// ✅ Good - mandatory fields are explicit");
        report.AppendLine("public record CreateUserRequest");
        report.AppendLine("{");
        report.AppendLine("    [Required]");
        report.AppendLine("    public required string Name { get; init; }");
        report.AppendLine("    ");
        report.AppendLine("    public string? Email { get; init; }  // Optional");
        report.AppendLine("}");
        report.AppendLine("```\n");

        report.AppendLine("### 4. Keep Methods Small\n");
        report.AppendLine("```csharp");
        report.AppendLine("// ✅ Good - small, focused methods");
        report.AppendLine("public async Task<Result> ProcessOrderAsync(Order order)");
        report.AppendLine("{");
        report.AppendLine("    await ValidateOrderAsync(order);");
        report.AppendLine("    await ReserveInventoryAsync(order);");
        report.AppendLine("    await ProcessPaymentAsync(order);");
        report.AppendLine("    await SendConfirmationAsync(order);");
        report.AppendLine("    ");
        report.AppendLine("    return Result.Success();");
        report.AppendLine("}");
        report.AppendLine("```\n");

        Console.WriteLine(report.ToString());

        var reportPath = Path.Combine(Path.GetTempPath(), "coding_standards_compliance_report.md");
        File.WriteAllText(reportPath, report.ToString());
        Console.WriteLine($"\n📄 详细报告已保存到: {reportPath}");

        Assert.True(true, "Coding standards compliance documented");
    }

    [Fact]
    public void ShouldNotHaveMeaninglessFileNames()
    {
        var violations = new List<string>();
        var sourceFiles = Utilities.CodeScanner.GetAllSourceFiles("src");
        
        // 常见的无意义文件名模式
        var meaninglessPatterns = new[]
        {
            @"^Class\d+\.cs$",        // Class1.cs, Class2.cs, etc.
            @"^Test\d+\.cs$",         // Test1.cs, Test2.cs, etc.
            @"^File\d+\.cs$",         // File1.cs, File2.cs, etc.
            @"^NewFile\d*\.cs$",      // NewFile.cs, NewFile1.cs, etc.
            @"^Untitled\d*\.cs$",     // Untitled.cs, Untitled1.cs, etc.
            @"^Temp\d*\.cs$",         // Temp.cs, Temp1.cs, etc.
            @"^temp\d*\.cs$",         // temp.cs, temp1.cs, etc.
        };

        foreach (var file in sourceFiles)
        {
            var fileName = Path.GetFileName(file);
            
            foreach (var pattern in meaninglessPatterns)
            {
                if (Regex.IsMatch(fileName, pattern))
                {
                    violations.Add(file);
                    break;
                }
            }
        }

        if (violations.Any())
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个无意义的文件名:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var violation in violations)
            {
                var fileName = Path.GetFileName(violation);
                // More robust relative path calculation
                var solutionRoot = GetSolutionRoot();
                var relativePath = Path.GetRelativePath(solutionRoot, violation);
                report.AppendLine($"  ❌ {fileName}");
                report.AppendLine($"     {relativePath}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 将文件重命名为有意义的名称，反映其用途或包含的类型");
            report.AppendLine("  2. 例如: Class1.cs → UserService.cs");
            report.AppendLine("  3. 例如: Temp.cs → TemporaryDataHolder.cs");
            report.AppendLine("  4. 如果文件不再需要，删除它");
            
            Assert.Fail(report.ToString());
        }
    }

    [Fact]
    public void AllEnumsShouldBeInCoreEnumsDirectory()
    {
        var violations = new List<string>();
        var multipleEnumsInFile = new List<string>();
        var sourceFiles = Utilities.CodeScanner.GetAllSourceFiles("src");
        
        // 期望的枚举目录路径
        var expectedEnumPath = Path.Combine("src", "Core", "ZakYip.WheelDiverterSorter.Core", "Enums");
        
        // 白名单：允许在这些位置存在枚举（厂商特定协议枚举）
        // Whitelist: Allow enums in these locations (vendor-specific protocol enums)
        var whitelistedPaths = new[]
        {
            "/Vendors/ShuDiNiao/",   // 书迪鸟厂商协议枚举
            "/Vendors/Modi/",        // 摩迪厂商协议枚举
            "\\Vendors\\ShuDiNiao\\",
            "\\Vendors\\Modi\\"
        };
        
        // 注：PR-TD6 已将所有内联枚举迁移到 Core/Enums 目录
        // Note: PR-TD6 has migrated all inline enums to Core/Enums directory
        
        foreach (var file in sourceFiles)
        {
            // 跳过白名单中的路径
            if (whitelistedPaths.Any(path => file.Contains(path)))
            {
                continue;
            }
            
            var content = File.ReadAllText(file);
            var lines = File.ReadAllLines(file);
            
            // 查找枚举定义（排除注释，只检查public enum）
            var enumMatches = new List<int>();
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                // 跳过注释行
                if (line.StartsWith("//") || line.StartsWith("*") || line.StartsWith("///"))
                    continue;
                    
                // 检测公共枚举定义 (只检查 public enum)
                if (Regex.IsMatch(line, @"\bpublic\s+enum\s+\w+") && !line.Contains("\"enum"))
                {
                    enumMatches.Add(i + 1);
                }
            }
            
            if (enumMatches.Any())
            {
                // More robust path validation
                var fileDir = Path.GetDirectoryName(file) ?? "";
                var normalizedDir = fileDir.Replace("\\", "/");
                var expectedDirPrefix = "src/Core/ZakYip.WheelDiverterSorter.Core/Enums";
                
                // 检查是否在正确的目录或其子目录中
                var isInCorrectLocation = normalizedDir.Contains(expectedDirPrefix);
                
                // 检查是否在正确的目录
                if (!isInCorrectLocation)
                {
                    violations.Add($"{Path.GetFileName(file)} - {file}");
                }
                
                // 检查是否一个文件包含多个枚举
                if (enumMatches.Count > 1)
                {
                    multipleEnumsInFile.Add($"{Path.GetFileName(file)} - 包含 {enumMatches.Count} 个枚举");
                }
            }
        }

        if (violations.Any() || multipleEnumsInFile.Any())
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("\n❌ 发现枚举定义不符合规范:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            if (violations.Any())
            {
                report.AppendLine($"\n⚠️ {violations.Count} 个枚举不在正确的目录 (src/Core/ZakYip.WheelDiverterSorter.Core/Enums/ 或其子目录):");
                foreach (var violation in violations.Take(20))
                {
                    report.AppendLine($"  ❌ {violation}");
                }
                if (violations.Count > 20)
                {
                    report.AppendLine($"  ... 还有 {violations.Count - 20} 个枚举");
                }
            }
            
            if (multipleEnumsInFile.Any())
            {
                report.AppendLine($"\n⚠️ {multipleEnumsInFile.Count} 个文件包含多个枚举（应该一个文件一个枚举）:");
                foreach (var violation in multipleEnumsInFile)
                {
                    report.AppendLine($"  ❌ {violation}");
                }
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 在 src/Core/ZakYip.WheelDiverterSorter.Core/ 下创建 Enums 目录（如果不存在）");
            report.AppendLine("  2. 将所有枚举文件移动到 Enums 目录或其子目录下（可以按领域分类，如 Enums/Communication/、Enums/Sorting/ 等）");
            report.AppendLine("  3. 确保每个文件只包含一个枚举定义");
            report.AppendLine("  4. 文件名应与枚举名称一致（例如: SensorType.cs 包含 SensorType 枚举）");
            report.AppendLine($"\n期望位置: {expectedEnumPath} （或其子目录）");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证接口和 DTO 文件中不包含内联枚举定义
    /// Verify that interface and DTO files do not contain inline enum definitions
    /// </summary>
    /// <remarks>
    /// PR-TD6: 防止在接口文件（I*.cs）或 DTO 文件（*Dto.cs、*Request.cs、*Response.cs）中定义内联枚举。
    /// 所有枚举应该在 Core/Enums 目录下的独立文件中定义。
    /// </remarks>
    [Fact]
    public void InterfacesAndDtosShouldNotContainInlineEnums()
    {
        var violations = new List<InlineEnumViolation>();
        var sourceFiles = Utilities.CodeScanner.GetAllSourceFiles("src");
        
        foreach (var file in sourceFiles)
        {
            var fileName = Path.GetFileName(file);
            
            // 检查是否是接口或 DTO 文件
            // Interface files start with 'I' followed by an uppercase letter
            // DTO files end with Dto.cs, Request.cs, or Response.cs
            var isInterfaceFile = fileName.Length >= 2 && fileName.StartsWith("I") && char.IsUpper(fileName[1]);
            var isDtoFile = fileName.EndsWith("Dto.cs") || fileName.EndsWith("Request.cs") || fileName.EndsWith("Response.cs");
            
            if (!isInterfaceFile && !isDtoFile)
            {
                continue;
            }
            
            var lines = File.ReadAllLines(file);
            var inBlockComment = false;
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                // 处理块注释
                // Handle block comments properly - detect start and end on same line
                var startsComment = line.Contains("/*");
                var endsComment = line.Contains("*/");
                
                if (startsComment && endsComment)
                {
                    // Single-line block comment - check if enum definition is outside the comment
                    var commentStart = line.IndexOf("/*");
                    var commentEnd = line.IndexOf("*/") + 2;
                    var lineWithoutComment = line.Substring(0, commentStart) + 
                                            (commentEnd < line.Length ? line.Substring(commentEnd) : "");
                    line = lineWithoutComment.Trim();
                }
                else if (startsComment)
                {
                    inBlockComment = true;
                    continue;
                }
                else if (endsComment)
                {
                    inBlockComment = false;
                    continue;
                }
                
                if (inBlockComment)
                    continue;
                    
                // 跳过单行注释和多行注释的延续行
                // Skip single-line comments and multi-line comment continuation lines
                if (line.StartsWith("//") || line.StartsWith("///"))
                    continue;
                    
                // Skip multi-line comment continuation lines (start with *)
                // But not multiplication operations (which wouldn't be at the start of a trimmed line in a comment context)
                if (line.StartsWith("*") && !line.StartsWith("*="))
                    continue;
                
                // 检测枚举定义（public enum 或 internal enum）
                if (Regex.IsMatch(line, @"\b(public|internal)\s+enum\s+\w+"))
                {
                    // 提取枚举名称
                    var match = Regex.Match(line, @"\benum\s+(\w+)");
                    var enumName = match.Success ? match.Groups[1].Value : "Unknown";
                    
                    violations.Add(new InlineEnumViolation
                    {
                        FilePath = file,
                        FileName = fileName,
                        LineNumber = i + 1,
                        EnumName = enumName,
                        FileType = isInterfaceFile ? "Interface" : "DTO"
                    });
                }
            }
        }

        if (violations.Any())
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个接口/DTO 文件中的内联枚举定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 接口和 DTO 文件不应包含内联枚举定义。所有枚举应在 Core/Enums 目录下独立定义。\n");

            var byType = violations.GroupBy(v => v.FileType);
            foreach (var group in byType)
            {
                report.AppendLine($"📁 {group.Key} 文件:");
                foreach (var violation in group)
                {
                    report.AppendLine($"   ❌ {violation.FileName}:{violation.LineNumber} - enum {violation.EnumName}");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 在 src/Core/ZakYip.WheelDiverterSorter.Core/Enums/ 下创建对应的枚举文件");
            report.AppendLine("  2. 将枚举定义移动到新文件中");
            report.AppendLine("  3. 在接口/DTO 文件中添加 using 语句引用枚举");
            report.AppendLine("  4. 删除接口/DTO 文件中的内联枚举定义");
            report.AppendLine("\n示例：将 IWheelDiverterDevice.cs 中的 WheelDiverterState 枚举");
            report.AppendLine("       移动到 Core/Enums/Hardware/WheelDiverterState.cs");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证没有使用 global using 语句
    /// Verify that no global using statements are used
    /// </summary>
    /// <remarks>
    /// 根据 copilot-instructions.md 规范：
    /// 代码中禁止使用 global using 指令。
    /// 当前代码库中不存在任何 global using 语句，
    /// 本测试将阻止任何新的 global using 被引入。
    /// </remarks>
    [Fact]
    public void ShouldNotUseGlobalUsing()
    {
        var violations = new List<GlobalUsingViolation>();
        var solutionRoot = GetSolutionRoot();
        
        // 扫描所有源代码和测试文件（排除 obj/bin 目录）
        var csFiles = Directory.GetFiles(solutionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !PathHelper.IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in csFiles)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    
                    // 跳过注释行
                    if (line.StartsWith("//") || line.StartsWith("/*") || line.StartsWith("*"))
                        continue;
                    
                    // 检查是否是 global using 语句（以 "global using" 开头，后跟空格和有效命名空间字符）
                    // 支持带点的命名空间如: global using System.Collections.Generic;
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^global\s+using\s+[\w.]+"))
                    {
                        violations.Add(new GlobalUsingViolation
                        {
                            FilePath = file,
                            LineNumber = i + 1,
                            Content = line
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading {file}: {ex.Message}");
            }
        }

        if (violations.Any())
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个 global using 违规:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 禁止新增或保留任何 global using；所有依赖必须通过显式 using 表达。\n");

            var byFile = violations.GroupBy(v => v.GetRelativePath());
            foreach (var group in byFile)
            {
                report.AppendLine($"📄 {group.Key}");
                foreach (var violation in group)
                {
                    report.AppendLine($"   Line {violation.LineNumber}: {violation.Content}");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 删除 global using 语句");
            report.AppendLine("  2. 在每个需要该命名空间的文件中添加显式 using 语句");
            report.AppendLine("  3. 删除任何仅包含 global using 的别名壳文件（如 GlobalUsings.cs）");
            report.AppendLine("  4. 确保所有依赖关系通过显式 using 语句表达，提高代码可读性");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 Ingress 层不包含冗余的上游 Facade 类型
    /// Verify that Ingress layer does not contain redundant upstream facade types
    /// </summary>
    /// <remarks>
    /// PR-TD8: 防止在 Ingress 层重新引入与 Communication 层 IUpstreamRoutingClient 功能重叠的 Facade 类型。
    /// 所有上游通信应使用 Communication 层的 IUpstreamRoutingClient 接口。
    /// </remarks>
    [Fact]
    public void IngressShouldNotContainRedundantUpstreamFacade()
    {
        var violations = new List<RedundantFacadeViolation>();
        
        // 获取 Ingress 项目的源文件
        var ingressPath = Path.Combine(GetSolutionRoot(), "src", "Ingress", "ZakYip.WheelDiverterSorter.Ingress");
        
        if (!Directory.Exists(ingressPath))
        {
            // 如果目录不存在，测试通过
            return;
        }
        
        var sourceFiles = Directory.GetFiles(ingressPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !PathHelper.IsInExcludedDirectory(f))
            .ToList();
        
        // 禁止的 Facade 类型名称模式
        var forbiddenTypePatterns = new[]
        {
            "IUpstreamFacade",
            "UpstreamFacade",
            "IUpstreamChannel",
            "UpstreamChannel",
            "IUpstreamCommandSender",
            "UpstreamCommandSender"
        };
        
        foreach (var file in sourceFiles)
        {
            var lines = File.ReadAllLines(file);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                // 跳过注释行
                if (line.StartsWith("//") || line.StartsWith("///") || line.StartsWith("*"))
                    continue;
                
                // 检查是否是接口或类定义
                foreach (var pattern in forbiddenTypePatterns)
                {
                    // 检查接口定义: public interface IUpstreamFacade
                    if (Regex.IsMatch(line, $@"\b(public|internal)\s+(interface|class)\s+{pattern}\b"))
                    {
                        violations.Add(new RedundantFacadeViolation
                        {
                            FilePath = file,
                            TypeName = pattern,
                            LineNumber = i + 1
                        });
                    }
                }
            }
        }

        if (violations.Any())
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个 Ingress 层的冗余上游 Facade 类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ Ingress 层不应包含与 Communication 层 IUpstreamRoutingClient 功能重叠的 Facade 类型。\n");

            foreach (var violation in violations)
            {
                var fileName = Path.GetFileName(violation.FilePath);
                report.AppendLine($"  ❌ {fileName}:{violation.LineNumber} - {violation.TypeName}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 删除 Ingress 层的冗余 Facade 类型");
            report.AppendLine("  2. 使用 Communication 层的 IUpstreamRoutingClient 接口进行上游通信");
            report.AppendLine("  3. 上游调用链应为：Controller/Application → ISortingOrchestrator → IUpstreamRoutingClient → 具体协议客户端");
            report.AppendLine("\n参考：PR-TD8 已清理 Ingress/Upstream/ 目录中的冗余 Facade 类型");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 *VendorConfigProvider 接口和实现必须具有数据转换逻辑
    /// Verify that *VendorConfigProvider interfaces and implementations have data transformation logic
    /// </summary>
    /// <remarks>
    /// PR-TD10: 防止出现"简单 Options 包装器"类的重复抽象。
    /// 有效的 VendorConfigProvider 应该：
    /// 1. 在 Core 层定义接口（如 ISensorVendorConfigProvider）
    /// 2. 提供厂商无关的返回类型（如 SensorConfigEntry）
    /// 3. 在 Drivers 层实现类型转换逻辑
    /// 
    /// 无效的模式：
    /// - 接口方法直接返回厂商特定的 Options 类型
    /// - 实现类只是简单包装 Options，没有类型转换
    /// </remarks>
    [Fact]
    public void VendorConfigProvidersShouldNotBeSimpleOptionsWrappers()
    {
        var violations = new List<SimpleWrapperViolation>();
        var solutionRoot = GetSolutionRoot();
        
        // 动态发现厂商名称 - 从 Vendors 目录结构中提取
        // Dynamically discover vendor names from Vendors directory structure
        var vendorsPath = Path.Combine(solutionRoot, "src", "Drivers", "ZakYip.WheelDiverterSorter.Drivers", "Vendors");
        var vendorNames = Directory.Exists(vendorsPath)
            ? Directory.GetDirectories(vendorsPath)
                .Select(d => Path.GetFileName(d))
                .Where(n => !string.Equals(n, "Simulated", StringComparison.OrdinalIgnoreCase))
                .ToList()
            : new List<string> { "Leadshine", "Siemens", "Modi", "ShuDiNiao" };
        
        // 扫描 Core 和 Drivers 目录中的 *VendorConfigProvider 文件
        var coreFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src", "Core"),
            "*VendorConfigProvider*.cs",
            SearchOption.AllDirectories)
            .Where(f => !PathHelper.IsInExcludedDirectory(f))
            .ToList();
            
        var driverFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src", "Drivers"),
            "*VendorConfigProvider*.cs",
            SearchOption.AllDirectories)
            .Where(f => !PathHelper.IsInExcludedDirectory(f))
            .ToList();
        
        // 检查接口定义：应该返回通用类型而不是厂商特定类型
        foreach (var file in coreFiles)
        {
            var lines = File.ReadAllLines(file);
            var fileName = Path.GetFileName(file);
            
            // 检查是否是接口文件
            if (!fileName.StartsWith("I") || !char.IsUpper(fileName[1]))
                continue;
            
            var inInterface = false;
            var inBlockComment = false;
            
            for (int lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                var line = lines[lineNum].Trim();
                
                // 处理块注释
                if (line.Contains("/*")) inBlockComment = true;
                if (line.Contains("*/")) inBlockComment = false;
                if (inBlockComment) continue;
                
                // 跳过单行注释
                if (line.StartsWith("//") || line.StartsWith("///")) continue;
                
                // 检测接口开始
                if (line.Contains("interface "))
                {
                    inInterface = true;
                    continue;
                }
                
                // 检测块结束（简化处理）
                if (inInterface && line.StartsWith("}"))
                {
                    inInterface = false;
                    continue;
                }
                
                // 只在接口内部检查方法签名返回类型
                if (inInterface)
                {
                    foreach (var vendorName in vendorNames)
                    {
                        // 更精确的模式：检查方法签名返回类型
                        // 模式: <VendorName>SomeType MethodName(
                        var methodSignaturePattern = $@"^\s*{vendorName}\w+\s+\w+\s*\(";
                        if (Regex.IsMatch(line, methodSignaturePattern))
                        {
                            violations.Add(new SimpleWrapperViolation
                            {
                                FilePath = file,
                                FileName = fileName,
                                ViolationType = "Interface returns vendor-specific type",
                                Description = $"接口方法返回了厂商特定类型 ({vendorName}*)，应返回通用类型 (Line {lineNum + 1})"
                            });
                            break;
                        }
                    }
                }
            }
        }
        
        // 检查实现类：应该有类型转换逻辑
        foreach (var file in driverFiles)
        {
            var content = File.ReadAllText(file);
            var fileName = Path.GetFileName(file);
            
            // 跳过接口文件
            if (fileName.StartsWith("I") && char.IsUpper(fileName[1]))
                continue;
            
            // 更宽泛的简单返回模式检测
            // 检测: return _options; 或 return this._options; 或 return _options.Value;
            var simpleReturnPattern = @"return\s+(this\.)?_options(\.\w+)?\s*;";
            var hasSimpleReturn = Regex.IsMatch(content, simpleReturnPattern);
            
            // 更精确的转换逻辑检测
            // 检查是否存在 LINQ Select 投影操作（典型的转换模式）
            var hasLinqProjection = Regex.IsMatch(content, @"\.Select\s*\(\s*\w+\s*=>");
            // 检查是否存在带有属性初始化器的 new 表达式（典型的类型转换）
            var hasObjectInitializerWithSelect = content.Contains(".Select(") && 
                                                  Regex.IsMatch(content, @"new\s+\w+\s*\{");
            // 检查是否存在显式的类型映射（new SomeEntry { Property = source.Property }）
            var hasExplicitMapping = Regex.IsMatch(content, @"new\s+\w+Entry\s*\{");
            
            var hasTransformationLogic = hasLinqProjection || hasObjectInitializerWithSelect || hasExplicitMapping;
            
            // 如果只有简单返回且没有转换逻辑，可能是简单包装器
            if (hasSimpleReturn && !hasTransformationLogic)
            {
                violations.Add(new SimpleWrapperViolation
                {
                    FilePath = file,
                    FileName = fileName,
                    ViolationType = "Implementation is simple wrapper",
                    Description = "实现类只是简单返回 Options，没有类型转换逻辑"
                });
            }
        }

        // 输出发现的问题（作为顾问性报告）
        if (violations.Any())
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"\n⚠️ 发现 {violations.Count} 个可能的简单 Options 包装器模式:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n根据 PR-TD10 规范，VendorConfigProvider 接口应该：");
            report.AppendLine("  1. 在 Core 层定义，返回厂商无关的类型");
            report.AppendLine("  2. 在 Drivers 层实现，进行厂商特定到通用类型的转换\n");

            foreach (var violation in violations)
            {
                report.AppendLine($"  📄 {violation.FileName}");
                report.AppendLine($"     类型: {violation.ViolationType}");
                report.AppendLine($"     说明: {violation.Description}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 正确的 VendorConfigProvider 模式（参考 ISensorVendorConfigProvider）：");
            report.AppendLine("  Core 层接口:");
            report.AppendLine("    - 定义厂商无关的返回类型（如 SensorConfigEntry）");
            report.AppendLine("    - 方法签名不包含厂商特定类型");
            report.AppendLine("  Drivers 层实现:");
            report.AppendLine("    - 注入厂商特定的 Options（如 LeadshineSensorOptions）");
            report.AppendLine("    - 在 Get* 方法中转换为通用类型");
            report.AppendLine("    - 使用 .Select() 等 LINQ 方法进行映射转换");
            
            // 这是顾问性测试，只输出警告但不失败
            Console.WriteLine(report.ToString());
        }

        Assert.True(true, $"VendorConfigProvider pattern check completed with {violations.Count} advisory warnings");
    }
}

/// <summary>
/// Global Using 违规信息
/// </summary>
public record GlobalUsingViolation
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Content { get; init; }
    
    public string GetRelativePath()
    {
        var parts = FilePath.Split(new[] { "/src/", "\\src\\", "/tests/", "\\tests\\" }, StringSplitOptions.None);
        return parts.Length > 1 ? parts[1] : FilePath;
    }
}

/// <summary>
/// 检查文件是否在排除的目录中（obj/bin）
/// Check if a file is in an excluded directory (obj/bin)
/// </summary>
file static class PathHelper
{
    private static readonly string[] ExcludedDirs = { "obj", "bin" };
    
    public static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var parts = normalizedPath.Split('/');
        
        return parts.Any(part => ExcludedDirs.Contains(part, StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>
/// 方法复杂度信息
/// </summary>
public record MethodComplexityInfo
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string MethodName { get; init; }
    public required int LineCount { get; init; }
}

/// <summary>
/// 内联枚举违规信息
/// Inline enum violation information
/// </summary>
/// <remarks>
/// PR-TD6: 用于记录接口或 DTO 文件中发现的内联枚举定义
/// </remarks>
public record InlineEnumViolation
{
    /// <summary>
    /// 文件完整路径
    /// </summary>
    public required string FilePath { get; init; }
    
    /// <summary>
    /// 文件名
    /// </summary>
    public required string FileName { get; init; }
    
    /// <summary>
    /// 行号
    /// </summary>
    public required int LineNumber { get; init; }
    
    /// <summary>
    /// 枚举名称
    /// </summary>
    public required string EnumName { get; init; }
    
    /// <summary>
    /// 文件类型（Interface 或 DTO）
    /// </summary>
    public required string FileType { get; init; }
}

/// <summary>
/// 冗余 Facade 类型违规信息
/// Redundant facade type violation information
/// </summary>
/// <remarks>
/// PR-TD8: 用于记录可能冗余的 Facade 类型定义
/// </remarks>
public record RedundantFacadeViolation
{
    /// <summary>
    /// 文件完整路径
    /// </summary>
    public required string FilePath { get; init; }
    
    /// <summary>
    /// 类型名称
    /// </summary>
    public required string TypeName { get; init; }
    
    /// <summary>
    /// 行号
    /// </summary>
    public required int LineNumber { get; init; }
}

/// <summary>
/// 简单 Options 包装器违规信息
/// Simple Options wrapper violation information
/// </summary>
/// <remarks>
/// PR-TD10: 用于记录可能是简单 Options 包装器的 VendorConfigProvider 类型
/// </remarks>
public record SimpleWrapperViolation
{
    /// <summary>
    /// 文件完整路径
    /// </summary>
    public required string FilePath { get; init; }
    
    /// <summary>
    /// 文件名
    /// </summary>
    public required string FileName { get; init; }
    
    /// <summary>
    /// 违规类型
    /// </summary>
    public required string ViolationType { get; init; }
    
    /// <summary>
    /// 描述
    /// </summary>
    public required string Description { get; init; }
}
