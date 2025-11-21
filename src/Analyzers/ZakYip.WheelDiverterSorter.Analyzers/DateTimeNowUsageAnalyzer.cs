using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZakYip.WheelDiverterSorter.Analyzers;

/// <summary>
/// 分析器：禁止业务代码直接使用 DateTime.Now / DateTime.UtcNow
/// Analyzer: Prohibit direct usage of DateTime.Now / DateTime.UtcNow in business code
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DateTimeNowUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ZAKYIP001";
    private const string Category = "Usage";

    private static readonly LocalizableString Title = 
        "禁止使用 DateTime.Now 或 DateTime.UtcNow - Do not use DateTime.Now or DateTime.UtcNow";
    
    private static readonly LocalizableString MessageFormat = 
        "禁止使用 '{0}'，请使用 ISystemClock.LocalNow 或 ISystemClock.UtcNow - Do not use '{0}', use ISystemClock.LocalNow or ISystemClock.UtcNow instead";
    
    private static readonly LocalizableString Description = 
        "业务代码应使用 ISystemClock 接口获取时间，便于测试和时区管理。仅 SystemClock 实现类可以直接使用 DateTime.Now/UtcNow。" +
        "Business code should use ISystemClock interface to get time for testability and timezone management. Only SystemClock implementation can directly use DateTime.Now/UtcNow.";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        
        // Check if it's DateTime.Now or DateTime.UtcNow
        if (memberAccess.Name.Identifier.Text != "Now" && 
            memberAccess.Name.Identifier.Text != "UtcNow")
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression);
        var symbol = symbolInfo.Symbol;

        if (symbol == null || symbol.ContainingNamespace?.ToDisplayString() != "System" ||
            symbol.Name != "DateTime")
        {
            return;
        }

        // Allow usage in SystemClock implementations
        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType != null)
        {
            var typeName = containingType.Name;
            if (typeName.Contains("SystemClock") || typeName.Contains("TestClock"))
            {
                return;
            }
        }

        // Report diagnostic
        var diagnostic = Diagnostic.Create(
            Rule,
            memberAccess.GetLocation(),
            $"DateTime.{memberAccess.Name.Identifier.Text}");
        
        context.ReportDiagnostic(diagnostic);
    }
}
