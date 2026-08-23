using iptv.Api.Utilities.MiddleWares;
using iptv.Services._IptvNotifier;
using Utilities.Attributes;

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

        public static void MapIptvHub(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHub<IptvHub>(IptvHub.Route)
                .WithMetadata(new IgnoreSignatureAttribute());
        }
    }
}