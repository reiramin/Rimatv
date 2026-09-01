using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Utilities.Constants;
using Utilities.Models.Settings;
using Utilities.Services;

namespace iptv.Services._BackgroundServices;

public class SelfPingScheduler(
    IServiceProvider serviceProvider,
    ILogger<SelfPingScheduler> logger)
    : SchedulerBase(serviceProvider, TimeSpan.FromMinutes(10)),
        RegisterMode.IHostedDependency
{
    protected override async Task HandleAsync(IServiceProvider scopedProvider)
    {
        try
        {
            var appSettings = scopedProvider.GetRequiredService<AppSettings>();

            if (string.IsNullOrWhiteSpace(appSettings.BaseUrl))
            {
                logger.LogWarning("SelfPing: BaseUrl is not configured.");
                return;
            }

            var httpClientFactory =
                scopedProvider.GetRequiredService<IHttpClientFactory>();

            using var client = httpClientFactory.CreateClient();

            client.Timeout = TimeSpan.FromSeconds(30);

            var response = await client.GetAsync(
                $"{appSettings.BaseUrl.TrimEnd('/')}/health");

            logger.LogInformation(
                "SelfPing: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SelfPing failed.");
        }
    }
}