using Microsoft.Extensions.DependencyInjection;
using Utilities.Constants;
using Utilities.Services;

namespace iptv.Services._Log._BackgroundServices
{
    public class LogCleanerScheduler(IServiceProvider serviceProvider)
        : SchedulerBase(serviceProvider, TimeSpan.FromDays(1)), RegisterMode.IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider)
        {
            var logService = scopedProvider.GetRequiredService<ILogService>();

            await logService.HardDeleteLogsLogsAsync();
            await logService.HardDeleteRequestLogsAsync();
        }
    }
}