using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZakYip.WheelDiverterSorter.Analyzers;

/// <summary>
/// 分析器：API Controller 必须返回 ApiResponse
/// Analyzer: API Controllers must return ApiResponse
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ApiControllerResponseTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ZAKYIP003";
    private const string Category = "Design";

    private static readonly LocalizableString Title = 
        "API Controller 方法必须返回 ApiResponse - API Controller methods must return ApiResponse";
    
    private static readonly LocalizableString MessageFormat = 
        "API Controller 的公开方法 '{0}' 必须返回 ApiResponse<T> 或 ActionResult<ApiResponse<T>> - " +
        "API Controller public method '{0}' must return ApiResponse<T> or ActionResult<ApiResponse<T>>";
    
    private static readonly LocalizableString Description = 
        "所有 API Controller 的公开 Action 方法必须返回统一的 ApiResponse 类型，以确保响应格式一致。" +
        "All API Controller public action methods must return the unified ApiResponse type to ensure consistent response format.";

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
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
        if (methodSymbol == null || !methodSymbol.DeclaredAccessibility.HasFlag(Accessibility.Public))
        {
            return;
        }

        // Check if the containing class derives from ControllerBase
        var containingType = methodSymbol.ContainingType;
        if (containingType == null || !InheritsFromControllerBase(containingType))
        {
            return;
        }

        // Skip constructors and special methods
        if (methodSymbol.MethodKind != MethodKind.Ordinary)
        {
            return;
        }

        // Check if the method has HTTP attribute (indicates it's an API endpoint)
        var hasHttpAttribute = methodSymbol.GetAttributes().Any(attr =>
        {
            var attrName = attr.AttributeClass?.Name;
            return attrName == "HttpGetAttribute" ||
                   attrName == "HttpPostAttribute" ||
                   attrName == "HttpPutAttribute" ||
                   attrName == "HttpDeleteAttribute" ||
                   attrName == "HttpPatchAttribute" ||
                   attrName == "HttpHeadAttribute" ||
                   attrName == "HttpOptionsAttribute";
        });

        // Only check methods with HTTP attributes or public methods in controllers
        if (!hasHttpAttribute && !IsLikelyApiAction(methodDeclaration))
        {
            return;
        }

        // Check the return type
        var returnType = methodSymbol.ReturnType;
        if (!IsValidReturnType(returnType))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name);
            
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool InheritsFromControllerBase(INamedTypeSymbol typeSymbol)
    {
        var currentType = typeSymbol.BaseType;
        while (currentType != null)
        {
            if (currentType.Name == "ControllerBase" || currentType.Name == "Controller")
            {
                return true;
            }
            currentType = currentType.BaseType;
        }
        return false;
    }

    private static bool IsValidReturnType(ITypeSymbol returnType)
    {
        // Task<T> or ValueTask<T> - unwrap to get T
        var actualType = returnType;
        if (returnType is INamedTypeSymbol namedType)
        {
            if (namedType.Name == "Task" || namedType.Name == "ValueTask")
            {
                if (namedType.TypeArguments.Length == 1)
                {
                    actualType = namedType.TypeArguments[0];
                }
            }
        }

        // Check if it's ApiResponse<T> or ActionResult<ApiResponse<T>>
        if (actualType is INamedTypeSymbol actualNamedType)
        {
            // Check for ApiResponse<T>
            if (actualNamedType.Name == "ApiResponse")
            {
                return true;
            }

            // Check for ActionResult<ApiResponse<T>> or IActionResult
            if (actualNamedType.Name == "ActionResult" || actualNamedType.Name == "IActionResult")
            {
                if (actualNamedType.TypeArguments.Length == 1)
                {
                    var innerType = actualNamedType.TypeArguments[0];
                    if (innerType is INamedTypeSymbol innerNamedType && 
                        innerNamedType.Name == "ApiResponse")
                    {
                        return true;
                    }
                }
                // Plain ActionResult/IActionResult without ApiResponse is not valid
                return false;
            }
        }

        return false;
    }

    private static bool IsLikelyApiAction(MethodDeclarationSyntax methodDeclaration)
    {
        // Public methods returning Task or IActionResult are likely API actions
        var returnTypeName = methodDeclaration.ReturnType.ToString();
        return returnTypeName.Contains("Task") || 
               returnTypeName.Contains("ActionResult") ||
               returnTypeName.Contains("IActionResult");
    }
}
