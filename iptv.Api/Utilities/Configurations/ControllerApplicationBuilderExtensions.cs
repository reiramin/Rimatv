using iptv.Api.Utilities.MiddleWares;

namespace iptv.Api.Utilities.Configurations
{
    public static class ApplicationControllerBuilderExtensions
    {
        public static void UseSecurityStamp(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<SecurityStampMiddleware>();
        }

        public static void UseLogger(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<LoggingMiddleware>();
        }
    }
}