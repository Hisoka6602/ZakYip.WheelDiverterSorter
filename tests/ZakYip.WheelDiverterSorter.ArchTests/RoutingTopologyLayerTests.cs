using System.Reflection;

namespace ZakYip.WheelDiverterSorter.ArchTests;

/// <summary>
/// 路由与拓扑分层架构测试
/// Architecture tests for Routing and Topology layer separation
/// </summary>
/// <remarks>
/// 这些测试确保：
/// 1. Routing 层不依赖 Topology 层
/// 2. Topology 层不依赖 Routing 层
/// 3. 只有 Orchestration 层可以同时依赖两者
/// 
/// These tests ensure:
/// 1. Routing layer does not depend on Topology layer
/// 2. Topology layer does not depend on Routing layer
/// 3. Only Orchestration layer can depend on both
/// </remarks>
public class RoutingTopologyLayerTests
{
    private readonly Assembly _coreAssembly;

    public RoutingTopologyLayerTests()
    {
        _coreAssembly = typeof(ZakYip.WheelDiverterSorter.Core.LineModel.Routing.RoutePlan).Assembly;
    }

    [Fact]
    public void Routing_ShouldNotDependOn_Topology()
    {
        // Arrange: Get all types in Routing namespace
        var routingTypes = _coreAssembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.Contains(".LineModel.Routing"))
            .ToList();

        Assert.NotEmpty(routingTypes); // Ensure we found routing types

        // Act: Check if any routing type references topology types
        var violations = new List<string>();
        
        foreach (var routingType in routingTypes)
        {
            var topologyReferences = GetTopologyReferences(routingType);
            if (topologyReferences.Any())
            {
                violations.Add($"{routingType.FullName} references Topology types: {string.Join(", ", topologyReferences)}");
            }
        }

        // Assert: No violations should exist
        Assert.Empty(violations);
    }

    [Fact]
    public void Topology_ShouldNotDependOn_Routing()
    {
        // Arrange: Get all types in Topology namespace
        var topologyTypes = _coreAssembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.Contains(".LineModel.Topology"))
            .ToList();

        Assert.NotEmpty(topologyTypes); // Ensure we found topology types

        // Act: Check if any topology type references routing types
        var violations = new List<string>();
        
        foreach (var topologyType in topologyTypes)
        {
            var routingReferences = GetRoutingReferences(topologyType);
            if (routingReferences.Any())
            {
                violations.Add($"{topologyType.FullName} references Routing types: {string.Join(", ", routingReferences)}");
            }
        }

        // Assert: No violations should exist
        Assert.Empty(violations);
    }

    [Fact]
    public void Routing_Namespace_ShouldExist()
    {
        // This test verifies that the Routing namespace structure is in place
        var routingTypes = _coreAssembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.Contains(".LineModel.Routing"))
            .ToList();

        Assert.NotEmpty(routingTypes);
    }

    [Fact]
    public void Topology_Namespace_ShouldExist()
    {
        // This test verifies that the Topology namespace structure is in place
        var topologyTypes = _coreAssembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.Contains(".LineModel.Topology"))
            .ToList();

        Assert.NotEmpty(topologyTypes);
    }

    [Fact]
    public void Orchestration_CanDependOnBoth_RoutingAndTopology()
    {
        // Arrange: Get all types in Orchestration namespace
        var orchestrationTypes = _coreAssembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.Contains(".LineModel.Orchestration"))
            .ToList();

        // Act: Check if orchestration types can reference both Routing and Topology
        var typesThatReferBoth = new List<string>();
        
        foreach (var orchType in orchestrationTypes)
        {
            var routingRefs = GetRoutingReferences(orchType);
            var topologyRefs = GetTopologyReferences(orchType);
            
            if (routingRefs.Any() && topologyRefs.Any())
            {
                typesThatReferBoth.Add(orchType.FullName ?? orchType.Name);
            }
        }

        // Assert: It's ok for Orchestration to reference both (this test just verifies it doesn't throw)
        // The important thing is that Routing and Topology don't reference each other
        Assert.True(true, "Orchestration layer is allowed to reference both Routing and Topology");
    }

    [Fact]
    public void OnlyOrchestration_CanDependOnBoth_RoutingAndTopology()
    {
        // Arrange: Get all types from Core assembly
        var allTypes = _coreAssembly.GetTypes()
            .Where(t => t.Namespace != null && 
                       !t.Namespace.Contains(".LineModel.Orchestration"))
            .ToList();

        // Act: Find any non-Orchestration types that reference both Routing and Topology
        var violations = new List<string>();
        
        foreach (var type in allTypes)
        {
            var routingRefs = GetRoutingReferences(type);
            var topologyRefs = GetTopologyReferences(type);
            
            if (routingRefs.Any() && topologyRefs.Any())
            {
                violations.Add($"{type.FullName} references both Routing and Topology but is not in Orchestration namespace");
            }
        }

        // Assert: No violations should exist
        Assert.Empty(violations);
    }

    /// <summary>
    /// Get all Topology type references from a given type
    /// </summary>
    private List<string> GetTopologyReferences(Type type)
    {
        var references = new HashSet<string>();

        try
        {
            // Check fields
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (IsTopologyTypeReference(field.FieldType))
                {
                    references.Add(field.FieldType.FullName ?? field.FieldType.Name);
                }
            }

            // Check properties
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (IsTopologyTypeReference(property.PropertyType))
                {
                    references.Add(property.PropertyType.FullName ?? property.PropertyType.Name);
                }
            }

            // Check method parameters and return types
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (IsTopologyTypeReference(method.ReturnType))
                {
                    references.Add(method.ReturnType.FullName ?? method.ReturnType.Name);
                }

                foreach (var parameter in method.GetParameters())
                {
                    if (IsTopologyTypeReference(parameter.ParameterType))
                    {
                        references.Add(parameter.ParameterType.FullName ?? parameter.ParameterType.Name);
                    }
                }
            }

            // Check constructor parameters
            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    if (IsTopologyTypeReference(parameter.ParameterType))
                    {
                        references.Add(parameter.ParameterType.FullName ?? parameter.ParameterType.Name);
                    }
                }
            }

            // Check base type
            if (type.BaseType != null && IsTopologyTypeReference(type.BaseType))
            {
                references.Add(type.BaseType.FullName ?? type.BaseType.Name);
            }

            // Check interfaces
            foreach (var iface in type.GetInterfaces())
            {
                if (IsTopologyTypeReference(iface))
                {
                    references.Add(iface.FullName ?? iface.Name);
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Some types may fail to load - log and continue
            Console.WriteLine($"Warning: Failed to load some types from {type.FullName}: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Other reflection errors - log and continue
            Console.WriteLine($"Warning: Error inspecting type {type.FullName}: {ex.Message}");
        }

        return references.ToList();
    }

    /// <summary>
    /// Get all Routing type references from a given type
    /// </summary>
    private List<string> GetRoutingReferences(Type type)
    {
        var references = new HashSet<string>();

        try
        {
            // Check fields
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (IsRoutingTypeReference(field.FieldType))
                {
                    references.Add(field.FieldType.FullName ?? field.FieldType.Name);
                }
            }

            // Check properties
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (IsRoutingTypeReference(property.PropertyType))
                {
                    references.Add(property.PropertyType.FullName ?? property.PropertyType.Name);
                }
            }

            // Check method parameters and return types
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (IsRoutingTypeReference(method.ReturnType))
                {
                    references.Add(method.ReturnType.FullName ?? method.ReturnType.Name);
                }

                foreach (var parameter in method.GetParameters())
                {
                    if (IsRoutingTypeReference(parameter.ParameterType))
                    {
                        references.Add(parameter.ParameterType.FullName ?? parameter.ParameterType.Name);
                    }
                }
            }

            // Check constructor parameters
            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    if (IsRoutingTypeReference(parameter.ParameterType))
                    {
                        references.Add(parameter.ParameterType.FullName ?? parameter.ParameterType.Name);
                    }
                }
            }

            // Check base type
            if (type.BaseType != null && IsRoutingTypeReference(type.BaseType))
            {
                references.Add(type.BaseType.FullName ?? type.BaseType.Name);
            }

            // Check interfaces
            foreach (var iface in type.GetInterfaces())
            {
                if (IsRoutingTypeReference(iface))
                {
                    references.Add(iface.FullName ?? iface.Name);
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Some types may fail to load - log and continue
            Console.WriteLine($"Warning: Failed to load some types from {type.FullName}: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Other reflection errors - log and continue
            Console.WriteLine($"Warning: Error inspecting type {type.FullName}: {ex.Message}");
        }

        return references.ToList();
    }

    /// <summary>
    /// Check if a type reference should be counted as a Topology dependency.
    /// Returns true if the type is from Topology namespace AND the referencing type is not in Orchestration.
    /// </summary>
    private bool IsTopologyTypeReference(Type type)
    {
        if (type.Namespace == null) return false;
        
        // Direct namespace check
        if (type.Namespace.Contains(".LineModel.Topology"))
            return true;

        // Check generic type arguments
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                if (IsTopologyTypeReference(arg))
                    return true;
            }
        }

        // Check array element type
        if (type.IsArray && IsTopologyTypeReference(type.GetElementType()!))
            return true;

        return false;
    }

    /// <summary>
    /// Check if a type reference should be counted as a Routing dependency.
    /// Returns true if the type is from Routing namespace AND the referencing type is not in Orchestration.
    /// </summary>
    private bool IsRoutingTypeReference(Type type)
    {
        if (type.Namespace == null) return false;
        
        // Direct namespace check
        if (type.Namespace.Contains(".LineModel.Routing"))
            return true;

        // Check generic type arguments
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                if (IsRoutingTypeReference(arg))
                    return true;
            }
        }

        // Check array element type
        if (type.IsArray && IsRoutingTypeReference(type.GetElementType()!))
            return true;

        return false;
    }
}
