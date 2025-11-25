using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.Swagger;

/// <summary>
/// Swagger Document Filter that dynamically shows/hides wheel diverter vendor-specific 
/// API endpoints based on the currently configured VendorType.
/// This ensures that only the relevant vendor configuration endpoints are shown in Swagger UI.
/// </summary>
public class WheelDiverterControllerDocumentFilter : IDocumentFilter
{
    private readonly IWheelDiverterConfigurationRepository _repository;

    public WheelDiverterControllerDocumentFilter(IWheelDiverterConfigurationRepository repository)
    {
        _repository = repository;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Get current wheel diverter configuration to determine active vendor
        WheelDiverterConfiguration currentConfig;
        try
        {
            currentConfig = _repository.Get();
        }
        catch
        {
            // If we can't get the configuration (e.g., during startup or if database is not initialized),
            // we fail gracefully by defaulting to ShuDiNiao. This ensures Swagger generation doesn't break.
            currentConfig = new WheelDiverterConfiguration { VendorType = WheelDiverterVendorType.ShuDiNiao };
        }

        var currentVendor = currentConfig.VendorType;

        // Define vendor-specific path prefixes
        var vendorPathPrefixes = new Dictionary<WheelDiverterVendorType, string>
        {
            { WheelDiverterVendorType.ShuDiNiao, "/api/config/wheeldiverter/shudiniao" },
            { WheelDiverterVendorType.Modi, "/api/config/wheeldiverter/modi" }
        };

        // Remove paths that don't match the current vendor
        var pathsToRemove = new List<string>();
        foreach (var path in swaggerDoc.Paths)
        {
            foreach (var vendorEntry in vendorPathPrefixes)
            {
                if (vendorEntry.Key == currentVendor)
                {
                    continue; // Keep current vendor's paths
                }

                if (path.Key.StartsWith(vendorEntry.Value, StringComparison.OrdinalIgnoreCase))
                {
                    pathsToRemove.Add(path.Key);
                }
            }
        }

        foreach (var pathToRemove in pathsToRemove)
        {
            swaggerDoc.Paths.Remove(pathToRemove);
        }

        // Also remove the vendor-specific tag if no paths remain
        var vendorTagsToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        switch (currentVendor)
        {
            case WheelDiverterVendorType.ShuDiNiao:
                vendorTagsToKeep.Add("数递鸟摆轮配置");
                break;
            case WheelDiverterVendorType.Modi:
                vendorTagsToKeep.Add("莫迪摆轮配置");
                break;
        }

        // Remove vendor tags that shouldn't be shown
        var tagsToRemove = swaggerDoc.Tags?
            .Where(t => (t.Name == "莫迪摆轮配置" || t.Name == "数递鸟摆轮配置") 
                        && !vendorTagsToKeep.Contains(t.Name))
            .ToList() ?? new List<OpenApiTag>();

        foreach (var tag in tagsToRemove)
        {
            swaggerDoc.Tags?.Remove(tag);
        }
    }
}
