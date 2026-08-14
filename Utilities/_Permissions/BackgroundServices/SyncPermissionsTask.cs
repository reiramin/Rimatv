// using M1Mentor.Utilities._Permissions.Contracts;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;
// using static M1Mentor.Utilities.Constants.RegisterMode;
//
// namespace Utilities._Permissions.BackgroundServices
// {
//     public class SyncPermissionsTask(ILogger<SyncPermissionsTask> logger, IServiceProvider serviceProvider)
//         : IHostedService, IHostedDependency
//     {
//         public async Task StartAsync(CancellationToken cancellationToken)
//         {
//             var newPermissions = M1Mentor.Utilities.Permissions.PermissionsList.Where(q => q.IsNew).ToList();
//
//             if (newPermissions.Count == 0)
//                 return;
//
//             logger.LogInformation($"------Syncing users permissions ...");
//
//
//             using var scope = serviceProvider.CreateScope();
//             var syncServices = scope.ServiceProvider.GetServices<IPermissionSyncService>().ToList();
//
//             try
//             {
//                 var tasks = syncServices.Select(s => s.SyncPermissionsAsync());
//                 await Task.WhenAll(tasks);
//
//                 logger.LogInformation($"------Permissions sync completed successfully.");
//             }
//             catch (Exception ex)
//             {
//                 logger.LogError(ex, $"------An error occurred while executing Syncing users permissions.");
//             }
//         }
//
//         public async Task StopAsync(CancellationToken cancellationToken)
//         {
//             await Task.CompletedTask;
//         }
//     }
// }
