using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZakYip.WheelDiverterSorter.Analyzers;

/// <summary>
/// 分析器：BackgroundService 的 ExecuteAsync 必须使用 SafeExecutionService
/// Analyzer: BackgroundService ExecuteAsync must use SafeExecutionService
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class BackgroundServiceSafeExecutionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ZAKYIP002";
    private const string Category = "Reliability";

    private static readonly LocalizableString Title = 
        "BackgroundService 必须使用 SafeExecutionService - BackgroundService must use SafeExecutionService";
    
    private static readonly LocalizableString MessageFormat = 
        "BackgroundService 的 ExecuteAsync 方法必须使用 ISafeExecutionService.ExecuteAsync 包裹主循环 - " +
        "BackgroundService ExecuteAsync must wrap main loop with ISafeExecutionService.ExecuteAsync";
    
    private static readonly LocalizableString Description = 
        "后台服务的主循环必须使用 SafeExecutionService 包裹，确保未捕获异常不会导致进程崩溃。" +
        "Background service main loops must be wrapped with SafeExecutionService to prevent uncaught exceptions from crashing the process.";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,  // Warning for now, can be upgraded to Error later
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        
        // Check if it's an ExecuteAsync method
        if (methodDeclaration.Identifier.Text != "ExecuteAsync")
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
        if (methodSymbol == null)
        {
            return;
        }

        // Check if the containing class derives from BackgroundService
        var containingType = methodSymbol.ContainingType;
        if (containingType == null || !InheritsFromBackgroundService(containingType))
        {
            return;
        }

        // Check if the method body contains a call to SafeExecutionService.ExecuteAsync
        if (methodDeclaration.Body == null && methodDeclaration.ExpressionBody == null)
        {
            return;
        }

        var hasSafeExecutionCall = false;
        
        if (methodDeclaration.Body != null)
        {
            var invocations = methodDeclaration.Body.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var invocationSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (invocationSymbol != null && 
                    invocationSymbol.Name == "ExecuteAsync" &&
                    invocationSymbol.ContainingType?.Name == "ISafeExecutionService")
                {
                    hasSafeExecutionCall = true;
                    break;
                }
            }
        }

        // If no SafeExecutionService call found, report diagnostic
        if (!hasSafeExecutionCall)
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                methodDeclaration.Identifier.GetLocation());
            
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool InheritsFromBackgroundService(INamedTypeSymbol typeSymbol)
    {
        var currentType = typeSymbol.BaseType;
        while (currentType != null)
        {
            if (currentType.Name == "BackgroundService")
            {
                return true;
            }
            currentType = currentType.BaseType;
        }
        return false;
    }
}
