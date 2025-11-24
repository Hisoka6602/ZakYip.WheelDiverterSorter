using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests.Utilities;

/// <summary>
/// 代码扫描工具 - 用于扫描源代码文件并提取违规信息
/// Code scanner utility for scanning source files and extracting violations
/// </summary>
public static class CodeScanner
{
    private static readonly string SolutionRoot = GetSolutionRoot();

    private static string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "ZakYip.WheelDiverterSorter.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new InvalidOperationException("Could not find solution root");
    }

    /// <summary>
    /// 扫描所有源代码文件
    /// Scan all source code files
    /// </summary>
    public static IEnumerable<string> GetAllSourceFiles(string directoryPattern = "src")
    {
        var searchPath = Path.Combine(SolutionRoot, directoryPattern);
        if (!Directory.Exists(searchPath))
        {
            return Enumerable.Empty<string>();
        }
        
        return Directory.GetFiles(searchPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/") && !f.Contains("\\obj\\") && !f.Contains("\\bin\\"));
    }

    /// <summary>
    /// 扫描测试代码文件
    /// Scan test code files
    /// </summary>
    public static IEnumerable<string> GetTestSourceFiles()
    {
        return GetAllSourceFiles("tests");
    }

    /// <summary>
    /// 查找文件中的 DateTime.Now/UtcNow 使用
    /// Find DateTime.Now/UtcNow usages in file
    /// </summary>
    public static List<DateTimeUsageViolation> FindDateTimeViolations(string filePath, bool allowUtcInWhitelist = false)
    {
        var violations = new List<DateTimeUsageViolation>();
        
        try
        {
            var lines = File.ReadAllLines(filePath);
            var fileContent = File.ReadAllText(filePath);

            // Check if this is SystemClock or TestClock implementation (whitelist)
            var isWhitelisted = fileContent.Contains("class LocalSystemClock") || 
                               fileContent.Contains("class SystemClock") ||
                               fileContent.Contains("class TestSystemClock") ||
                               fileContent.Contains("class MockSystemClock") ||
                               filePath.Contains("/Analyzers/") || 
                               filePath.Contains("\\Analyzers\\");

            if (isWhitelisted)
            {
                return violations;
            }

            // Regex patterns for DateTime violations
            var dateTimeNowPattern = new Regex(@"\bDateTime\.Now\b", RegexOptions.Compiled);
            var dateTimeUtcNowPattern = new Regex(@"\bDateTime\.UtcNow\b", RegexOptions.Compiled);
            var dateTimeOffsetUtcNowPattern = new Regex(@"\bDateTimeOffset\.UtcNow\b", RegexOptions.Compiled);
            var clockUtcNowPattern = new Regex(@"\b_clock\.UtcNow\b|\bISystemClock.*\.UtcNow\b", RegexOptions.Compiled);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNumber = i + 1;

                // Skip comments
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("//") || trimmedLine.StartsWith("*") || trimmedLine.StartsWith("///"))
                {
                    continue;
                }

                // Check for DateTime.Now
                if (dateTimeNowPattern.IsMatch(line))
                {
                    violations.Add(new DateTimeUsageViolation
                    {
                        FilePath = filePath,
                        LineNumber = lineNumber,
                        Usage = "DateTime.Now",
                        CodeSnippet = line.Trim(),
                        Severity = ViolationSeverity.Error
                    });
                }

                // Check for DateTime.UtcNow
                if (dateTimeUtcNowPattern.IsMatch(line))
                {
                    violations.Add(new DateTimeUsageViolation
                    {
                        FilePath = filePath,
                        LineNumber = lineNumber,
                        Usage = "DateTime.UtcNow",
                        CodeSnippet = line.Trim(),
                        Severity = ViolationSeverity.Error
                    });
                }

                // Check for DateTimeOffset.Now
                if (dateTimeOffsetUtcNowPattern.IsMatch(line))
                {
                    violations.Add(new DateTimeUsageViolation
                    {
                        FilePath = filePath,
                        LineNumber = lineNumber,
                        Usage = "DateTimeOffset.Now",
                        CodeSnippet = line.Trim(),
                        Severity = ViolationSeverity.Error
                    });
                }

                // Check for _clock.UtcNow (should use LocalNow instead)
                if (!allowUtcInWhitelist && clockUtcNowPattern.IsMatch(line))
                {
                    violations.Add(new DateTimeUsageViolation
                    {
                        FilePath = filePath,
                        LineNumber = lineNumber,
                        Usage = "ISystemClock.UtcNow",
                        CodeSnippet = line.Trim(),
                        Severity = ViolationSeverity.Warning
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning {filePath}: {ex.Message}");
        }

        return violations;
    }

    /// <summary>
    /// 扫描所有文件的 DateTime 违规
    /// Scan all files for DateTime violations
    /// </summary>
    public static List<DateTimeUsageViolation> ScanAllDateTimeViolations(bool includeTests = false, bool allowUtcInWhitelist = false)
    {
        var violations = new List<DateTimeUsageViolation>();
        var files = GetAllSourceFiles("src").ToList();
        
        if (includeTests)
        {
            files.AddRange(GetTestSourceFiles());
        }

        foreach (var file in files)
        {
            violations.AddRange(FindDateTimeViolations(file, allowUtcInWhitelist));
        }

        return violations.OrderBy(v => v.FilePath).ThenBy(v => v.LineNumber).ToList();
    }

    /// <summary>
    /// 查找 BackgroundService 实现
    /// Find BackgroundService implementations
    /// </summary>
    public static List<BackgroundServiceInfo> FindBackgroundServices()
    {
        var services = new List<BackgroundServiceInfo>();
        var files = GetAllSourceFiles("src");

        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file);
                var lines = File.ReadAllLines(file);

                // Simple regex to find BackgroundService inheritance
                if (!content.Contains(": BackgroundService"))
                {
                    continue;
                }

                // Extract class name
                var classMatch = Regex.Match(content, @"class\s+(\w+)\s*:\s*BackgroundService");
                if (!classMatch.Success)
                {
                    continue;
                }

                var className = classMatch.Groups[1].Value;

                // Check if ExecuteAsync has SafeExecution
                var hasSafeExecution = content.Contains("SafeExecutionService") ||
                                      content.Contains("_safeExecutor.ExecuteAsync") ||
                                      content.Contains("_safeExecution.ExecuteAsync") ||
                                      content.Contains("ISafeExecutionService");

                services.Add(new BackgroundServiceInfo
                {
                    FilePath = file,
                    ClassName = className,
                    HasSafeExecution = hasSafeExecution
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning {file}: {ex.Message}");
            }
        }

        return services;
    }

    /// <summary>
    /// 查找非线程安全集合使用
    /// Find non-thread-safe collection usages
    /// </summary>
    public static List<CollectionUsageInfo> FindNonThreadSafeCollections()
    {
        var usages = new List<CollectionUsageInfo>();
        var files = GetAllSourceFiles("src");

        // High-risk namespaces that require thread-safe collections
        var highRiskNamespaces = new[]
        {
            "Execution",
            "Communication",
            "Observability",
            "Simulation"
        };

        foreach (var file in files)
        {
            try
            {
                // Check if file is in high-risk namespace
                var isHighRisk = highRiskNamespaces.Any(ns => file.Contains($"/{ns}/") || file.Contains($"\\{ns}\\"));
                
                if (!isHighRisk)
                {
                    continue; // Skip low-risk files
                }

                var lines = File.ReadAllLines(file);
                var content = File.ReadAllText(file);

                // Extract class name
                var classMatch = Regex.Match(content, @"class\s+(\w+)");
                var className = classMatch.Success ? classMatch.Groups[1].Value : "Unknown";

                // Pattern for field declarations with non-thread-safe collections
                var fieldPattern = new Regex(
                    @"private\s+(?:readonly\s+)?(?<type>Dictionary<[^>]+>|List<[^>]+>|HashSet<[^>]+>|Queue<[^>]+>|Stack<[^>]+>)\s+(?<name>\w+)",
                    RegexOptions.Compiled | RegexOptions.ExplicitCapture);

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var match = fieldPattern.Match(line);
                    
                    if (match.Success)
                    {
                        var collectionType = match.Groups["type"].Value;
                        var fieldName = match.Groups["name"].Value;

                        // Check for thread-safe alternatives
                        var isThreadSafe = collectionType.Contains("Concurrent") ||
                                          collectionType.Contains("Immutable") ||
                                          line.Contains("IReadOnly");

                        // Check for [SingleThreadedOnly] attribute
                        var hasSingleThreadedAttribute = i > 0 && lines[i - 1].Contains("[SingleThreadedOnly]");

                        if (!isThreadSafe && !hasSingleThreadedAttribute)
                        {
                            usages.Add(new CollectionUsageInfo
                            {
                                FilePath = file,
                                LineNumber = i + 1,
                                ClassName = className,
                                FieldName = fieldName,
                                CollectionType = collectionType,
                                IsMarkedSafe = false
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning {file}: {ex.Message}");
            }
        }

        return usages.OrderBy(u => u.FilePath).ThenBy(u => u.LineNumber).ToList();
    }
}

/// <summary>
/// 违规严重程度
/// Violation severity
/// </summary>
public enum ViolationSeverity
{
    Warning,
    Error
}

/// <summary>
/// DateTime 使用违规信息
/// DateTime usage violation info
/// </summary>
public record DateTimeUsageViolation
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Usage { get; init; }
    public required string CodeSnippet { get; init; }
    public required ViolationSeverity Severity { get; init; }
    
    public string GetRelativePath()
    {
        var parts = FilePath.Split(new[] { "/src/", "\\src\\" }, StringSplitOptions.None);
        return parts.Length > 1 ? "src/" + parts[1] : FilePath;
    }
}

/// <summary>
/// BackgroundService 信息
/// BackgroundService info
/// </summary>
public record BackgroundServiceInfo
{
    public required string FilePath { get; init; }
    public required string ClassName { get; init; }
    public required bool HasSafeExecution { get; init; }
    
    public string GetRelativePath()
    {
        var parts = FilePath.Split(new[] { "/src/", "\\src\\" }, StringSplitOptions.None);
        return parts.Length > 1 ? "src/" + parts[1] : FilePath;
    }
}

/// <summary>
/// 集合使用信息
/// Collection usage info
/// </summary>
public record CollectionUsageInfo
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string ClassName { get; init; }
    public required string FieldName { get; init; }
    public required string CollectionType { get; init; }
    public required bool IsMarkedSafe { get; init; }
    
    public string GetRelativePath()
    {
        var parts = FilePath.Split(new[] { "/src/", "\\src\\" }, StringSplitOptions.None);
        return parts.Length > 1 ? "src/" + parts[1] : FilePath;
    }
}
