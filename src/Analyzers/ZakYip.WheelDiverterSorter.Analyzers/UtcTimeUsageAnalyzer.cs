using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZakYip.WheelDiverterSorter.Analyzers;

/// <summary>
/// 分析器：禁止业务代码使用 UTC 时间（仅允许与外部系统交互时使用）
/// Analyzer: Prohibit business code from using UTC time (only allow for external system interaction)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UtcTimeUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ZAKYIP004";
    private const string Category = "Usage";

    private static readonly LocalizableString Title = 
        "避免使用 UTC 时间 - Avoid using UTC time";
    
    private static readonly LocalizableString MessageFormat = 
        "业务代码应使用 ISystemClock.LocalNow 而不是 {0}。UTC 时间仅用于与外部系统协议交互。" +
        "Business code should use ISystemClock.LocalNow instead of {0}. UTC time is only for external system protocol interaction.";
    
    private static readonly LocalizableString Description = 
        "系统内部所有业务时间使用本地时间。UTC 时间仅在与外部系统交互且协议明确要求时使用。" +
        "All internal business times use local time. UTC time is only used when interacting with external systems where the protocol explicitly requires it.";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning, // Use Warning instead of Error for more flexibility
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        
        // Check if it's accessing UtcNow
        if (memberAccess.Name.Identifier.Text != "UtcNow")
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess.Expression);
        var symbol = symbolInfo.Symbol;

        if (symbol == null)
        {
            return;
        }

        // Check if it's DateTime.UtcNow (handled by DateTimeNowUsageAnalyzer)
        if (symbol.ContainingNamespace?.ToDisplayString() == "System" && symbol.Name == "DateTime")
        {
            return; // Already handled by ZAKYIP001
        }

        // Check if it's DateTimeOffset.UtcNow
        if (symbol.ContainingNamespace?.ToDisplayString() == "System" && symbol.Name == "DateTimeOffset")
        {
            if (!ShouldAllowUtcUsage(context))
            {
                ReportDiagnostic(context, memberAccess, "DateTimeOffset.UtcNow");
            }
            return;
        }

        // Check if it's ISystemClock.UtcNow
        var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
        var type = typeInfo.Type;
        
        if (type == null)
        {
            return;
        }

        // Check if the type is ISystemClock or implements ISystemClock
        bool isSystemClock = type.Name == "ISystemClock" || 
                            type.AllInterfaces.Any(i => i.Name == "ISystemClock");

        if (isSystemClock && !ShouldAllowUtcUsage(context))
        {
            ReportDiagnostic(context, memberAccess, "ISystemClock.UtcNow");
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        // Check for ToUniversalTime() or ToUniversalTime(DateTimeKind)
        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName != "ToUniversalTime")
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        // Check if it's DateTime.ToUniversalTime()
        if (methodSymbol.ContainingType?.Name == "DateTime" && 
            methodSymbol.ContainingNamespace?.ToDisplayString() == "System")
        {
            if (!ShouldAllowUtcUsage(context))
            {
                ReportDiagnostic(context, invocation, "DateTime.ToUniversalTime()");
            }
            return;
        }

        // Check if it's DateTimeOffset.ToUniversalTime()
        if (methodSymbol.ContainingType?.Name == "DateTimeOffset" && 
            methodSymbol.ContainingNamespace?.ToDisplayString() == "System")
        {
            if (!ShouldAllowUtcUsage(context))
            {
                ReportDiagnostic(context, invocation, "DateTimeOffset.ToUniversalTime()");
            }
        }
    }

    private static bool ShouldAllowUtcUsage(SyntaxNodeAnalysisContext context)
    {
        var containingNamespace = context.ContainingSymbol?.ContainingNamespace?.ToDisplayString();
        if (containingNamespace == null)
        {
            return false;
        }

        // Allow in Communication infrastructure (for external system protocols)
        if (containingNamespace.Contains(".Communication."))
        {
            return true;
        }
        
        // Allow in test code
        if (containingNamespace.Contains(".Tests") || containingNamespace.Contains(".Test"))
        {
            return true;
        }

        return false;
    }

    private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, SyntaxNode node, string usage)
    {
        var diagnostic = Diagnostic.Create(
            Rule,
            node.GetLocation(),
            usage);
        
        context.ReportDiagnostic(diagnostic);
    }
}
