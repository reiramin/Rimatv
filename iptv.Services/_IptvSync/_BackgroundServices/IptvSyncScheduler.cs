using iptv.Services._IptvSync.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Utilities.Constants;
using Utilities.Services;

namespace iptv.Services._IptvSync._BackgroundServices;

public class IptvSyncScheduler(IServiceProvider serviceProvider)
    : SchedulerBase(serviceProvider, TimeSpan.FromMinutes(30)), RegisterMode.IHostedDependency
{
    protected override async Task HandleAsync(IServiceProvider scopedProvider)
    {
        var syncService = scopedProvider.GetRequiredService<IIptvSyncService>();

        await syncService.SyncAllProvidersAsync();
    }
}
