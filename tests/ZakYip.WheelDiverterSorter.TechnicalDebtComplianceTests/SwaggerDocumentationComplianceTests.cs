using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Xunit;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// Swagger文档合规性测试 - Swagger Documentation Compliance Tests
/// </summary>
/// <remarks>
/// 确保所有API端点都有完整的中文Swagger注释
/// Ensures all API endpoints have complete Chinese Swagger documentation
/// </remarks>
public class SwaggerDocumentationComplianceTests
{
    /// <summary>
    /// 所有Controller类必须有XML注释
    /// All Controller classes must have XML comments
    /// </summary>
    [Fact]
    public void AllControllers_ShouldHaveXmlDocumentation()
    {
        // Arrange
        var hostAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Host");
        var controllerTypes = hostAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller"))
            .Where(t => t.Namespace != null && t.Namespace.Contains("Controllers"))
            .ToList();

        // Act & Assert
        Assert.NotEmpty(controllerTypes);
        
        var controllersWithoutDocs = new List<string>();
        foreach (var controller in controllerTypes)
        {
            // Check if controller has at least some public methods
            var publicMethods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (publicMethods.Length == 0) continue;

            // Controllers should ideally have XML documentation
            // We can't directly check for /// comments at runtime, but we can check for SwaggerOperation attributes
            var methodsWithoutSwagger = publicMethods
                .Where(m => m.GetCustomAttribute<HttpGetAttribute>() != null ||
                           m.GetCustomAttribute<HttpPostAttribute>() != null ||
                           m.GetCustomAttribute<HttpPutAttribute>() != null ||
                           m.GetCustomAttribute<HttpDeleteAttribute>() != null ||
                           m.GetCustomAttribute<HttpPatchAttribute>() != null)
                .Where(m => m.GetCustomAttribute<SwaggerOperationAttribute>() == null)
                .ToList();

            if (methodsWithoutSwagger.Any())
            {
                controllersWithoutDocs.Add($"{controller.Name}: {string.Join(", ", methodsWithoutSwagger.Select(m => m.Name))}");
            }
        }

        Assert.Empty(controllersWithoutDocs);
    }

    /// <summary>
    /// 所有API端点必须有SwaggerOperation特性
    /// All API endpoints must have SwaggerOperation attribute
    /// </summary>
    [Fact]
    public void AllApiEndpoints_ShouldHaveSwaggerOperationAttribute()
    {
        // Arrange
        var hostAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Host");
        var controllerTypes = hostAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller"))
            .Where(t => t.Namespace != null && t.Namespace.Contains("Controllers"))
            .ToList();

        // Act
        var endpointsWithoutSwaggerOperation = new List<string>();
        foreach (var controller in controllerTypes)
        {
            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                // Check if this is an HTTP endpoint
                var hasHttpMethod = method.GetCustomAttribute<HttpGetAttribute>() != null ||
                                   method.GetCustomAttribute<HttpPostAttribute>() != null ||
                                   method.GetCustomAttribute<HttpPutAttribute>() != null ||
                                   method.GetCustomAttribute<HttpDeleteAttribute>() != null ||
                                   method.GetCustomAttribute<HttpPatchAttribute>() != null;

                if (hasHttpMethod)
                {
                    var swaggerOp = method.GetCustomAttribute<SwaggerOperationAttribute>();
                    if (swaggerOp == null)
                    {
                        endpointsWithoutSwaggerOperation.Add($"{controller.Name}.{method.Name}");
                    }
                }
            }
        }

        // Assert
        Assert.Empty(endpointsWithoutSwaggerOperation);
    }

    /// <summary>
    /// 所有SwaggerOperation必须包含Summary和Description
    /// All SwaggerOperation attributes must include Summary and Description
    /// </summary>
    [Fact]
    public void AllSwaggerOperations_ShouldHaveSummaryAndDescription()
    {
        // Arrange
        var hostAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Host");
        var controllerTypes = hostAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller"))
            .Where(t => t.Namespace != null && t.Namespace.Contains("Controllers"))
            .ToList();

        // Act
        var endpointsWithIncompleteSwagger = new List<string>();
        foreach (var controller in controllerTypes)
        {
            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var swaggerOp = method.GetCustomAttribute<SwaggerOperationAttribute>();
                if (swaggerOp != null &&
                    (string.IsNullOrWhiteSpace(swaggerOp.Summary) ||
                     string.IsNullOrWhiteSpace(swaggerOp.Description)))
                {
                    endpointsWithIncompleteSwagger.Add(
                        $"{controller.Name}.{method.Name}: " +
                        $"Summary={(string.IsNullOrWhiteSpace(swaggerOp.Summary) ? "missing" : "ok")}, " +
                        $"Description={(string.IsNullOrWhiteSpace(swaggerOp.Description) ? "missing" : "ok")}");
                }
            }
        }

        // Assert
        Assert.Empty(endpointsWithIncompleteSwagger);
    }

    /// <summary>
    /// 所有SwaggerOperation的Summary必须包含中文字符
    /// All SwaggerOperation Summary must contain Chinese characters
    /// </summary>
    [Fact]
    public void AllSwaggerOperations_ShouldHaveChineseSummary()
    {
        // Arrange
        var hostAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Host");
        var controllerTypes = hostAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller"))
            .Where(t => t.Namespace != null && t.Namespace.Contains("Controllers"))
            .ToList();

        // Act
        var endpointsWithoutChineseSummary = new List<string>();
        foreach (var controller in controllerTypes)
        {
            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var swaggerOp = method.GetCustomAttribute<SwaggerOperationAttribute>();
                if (swaggerOp != null && !string.IsNullOrWhiteSpace(swaggerOp.Summary) && !ContainsChinese(swaggerOp.Summary))
                {
                    endpointsWithoutChineseSummary.Add($"{controller.Name}.{method.Name}: {swaggerOp.Summary}");
                }
            }
        }

        // Assert
        Assert.Empty(endpointsWithoutChineseSummary);
    }

    /// <summary>
    /// 所有API端点必须有SwaggerResponse特性标注可能的响应
    /// All API endpoints must have SwaggerResponse attributes for possible responses
    /// </summary>
    [Fact]
    public void AllApiEndpoints_ShouldHaveSwaggerResponseAttributes()
    {
        // Arrange
        var hostAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Host");
        var controllerTypes = hostAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller"))
            .Where(t => t.Namespace != null && t.Namespace.Contains("Controllers"))
            .ToList();

        // Act
        var endpointsWithoutSwaggerResponse = new List<string>();
        foreach (var controller in controllerTypes)
        {
            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                // Check if this is an HTTP endpoint
                var hasHttpMethod = method.GetCustomAttribute<HttpGetAttribute>() != null ||
                                   method.GetCustomAttribute<HttpPostAttribute>() != null ||
                                   method.GetCustomAttribute<HttpPutAttribute>() != null ||
                                   method.GetCustomAttribute<HttpDeleteAttribute>() != null ||
                                   method.GetCustomAttribute<HttpPatchAttribute>() != null;

                if (hasHttpMethod)
                {
                    var swaggerResponses = method.GetCustomAttributes<SwaggerResponseAttribute>();
                    if (!swaggerResponses.Any())
                    {
                        endpointsWithoutSwaggerResponse.Add($"{controller.Name}.{method.Name}");
                    }
                }
            }
        }

        // Assert
        Assert.Empty(endpointsWithoutSwaggerResponse);
    }

    /// <summary>
    /// 检查字符串是否包含中文字符
    /// Check if string contains Chinese characters
    /// </summary>
    private static bool ContainsChinese(string text)
    {
        return text.Any(c => c >= 0x4E00 && c <= 0x9FFF);
    }
}
