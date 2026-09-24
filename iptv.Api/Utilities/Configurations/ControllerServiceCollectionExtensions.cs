using iptv.Services._Common.Settings;
using Microsoft.Extensions.Options;
using Utilities.Constants;
using Utilities.Models.Settings;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;

namespace iptv.Api.Utilities.Configurations
{
    public static class ControllerServiceCollectionExtensions
    {
        public static void AddSettings(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClient();

            services.AddSignalR();

            services.RegisterSetting<MonjoSettings, IMonjoSettings>(configuration.GetSection(nameof(MonjoSettings)));

            services.RegisterSetting<ApplicationPoolSettings>(configuration.GetSection(nameof(ApplicationPoolSettings)));

            services.RegisterSetting<JwtServiceSettings>(configuration.GetSection(nameof(JwtServiceSettings)));

            services.RegisterSetting<FirewallSettings>(configuration.GetSection(nameof(FirewallSettings)));

            // services.RegisterSetting<CaptchaSettings>(configuration.GetSection(nameof(CaptchaSettings)));

            // services.RegisterSetting<EmailSettings>(configuration.GetSection(nameof(EmailSettings)));

            services.RegisterSetting<AppSettings>(configuration.GetSection(nameof(AppSettings)));

            services.RegisterSetting<ClientSettings>(configuration.GetSection("Client"));
            services.RegisterSetting<RelaySettings>(configuration.GetSection("Relay"));
            services.RegisterSetting<SyncSettings>(configuration.GetSection("Sync"));
            services.RegisterSetting<ReportSettings>(configuration.GetSection("Reports"));

        }

        private static void RegisterSetting<TSettings>(this IServiceCollection services, IConfigurationSection configuration)
           where TSettings : class, new()
        {
            services.Configure<TSettings>(configuration);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<TSettings>>().Value);
        }

        private static void RegisterSetting<TSettings, TISettings>(this IServiceCollection services, IConfigurationSection configuration)
            where TISettings : class
            where TSettings : class, TISettings, new()
        {
            services.Configure<TSettings>(configuration);
            services.AddSingleton<TISettings>(sp => sp.GetRequiredService<IOptions<TSettings>>().Value);
        }
    }
}
