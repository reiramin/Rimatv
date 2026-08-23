using iptv.Services._StreamHealth.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Utilities.Constants;
using Utilities.Services;

namespace iptv.Services._StreamHealth._BackgroundServices;

public class StreamHealthScheduler(IServiceProvider serviceProvider)
    : SchedulerBase(serviceProvider, TimeSpan.FromMinutes(5)), RegisterMode.IHostedDependency
{
    protected override async Task HandleAsync(IServiceProvider scopedProvider)
    {
        var streamHealthService = scopedProvider.GetRequiredService<IStreamHealthService>();

        await streamHealthService.CheckStaleStreamsAsync();
    }
}
