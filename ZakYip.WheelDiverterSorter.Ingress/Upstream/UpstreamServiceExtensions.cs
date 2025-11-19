using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Ingress.Upstream.Configuration;
using ZakYip.WheelDiverterSorter.Ingress.Upstream.Http;

namespace ZakYip.WheelDiverterSorter.Ingress.Upstream;

/// <summary>
/// 上游服务扩展
/// </summary>
public static class UpstreamServiceExtensions
{
    /// <summary>
    /// 添加上游服务到依赖注入容器
    /// </summary>
    public static IServiceCollection AddUpstreamServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册配置
        services.Configure<IngressOptions>(configuration.GetSection(IngressOptions.SectionName));

        // 注册 HttpClient
        services.AddHttpClient();

        // 注册通道和命令发送器
        services.AddSingleton<IUpstreamChannel, HttpUpstreamChannel>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("UpstreamHttp");
            var logger = sp.GetService<ILogger<HttpUpstreamChannel>>();

            // 从配置中读取 HTTP 通道配置
            var ingressOptions = configuration.GetSection(IngressOptions.SectionName).Get<IngressOptions>();
            var httpChannelConfig = ingressOptions?.UpstreamChannels
                .FirstOrDefault(c => c.Type.Equals("HTTP", StringComparison.OrdinalIgnoreCase));

            if (httpChannelConfig == null)
            {
                // 默认配置
                httpChannelConfig = new UpstreamChannelConfig
                {
                    Name = "DefaultHttp",
                    Type = "HTTP",
                    Endpoint = "http://localhost:5000",
                    TimeoutMs = 5000,
                    Priority = 100,
                    Enabled = true
                };
            }

            return new HttpUpstreamChannel(httpClient, httpChannelConfig, logger);
        });

        services.AddSingleton<IUpstreamCommandSender>(sp =>
        {
            var channel = sp.GetRequiredService<IUpstreamChannel>();
            return (IUpstreamCommandSender)channel;
        });

        // 注册门面
        services.AddSingleton<IUpstreamFacade, UpstreamFacade>();

        return services;
    }
}
