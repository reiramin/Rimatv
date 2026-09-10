using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Utilities.Middlewares;

namespace Utilities.Configuration
{
    public static class ApplicationBuilderExtensions
    {
        public static void UseHsts(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (!env.IsDevelopment())
            {
                app.UseHsts();
            }
        }

        public static void UseDeveloperExceptionPage(
            this IApplicationBuilder app,
            IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
        }

        public static void UseEndpoints(this IApplicationBuilder app)
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        public static void UseCustomExceptionHandler(
            this IApplicationBuilder builder)
        {
            builder.UseMiddleware<CustomExceptionHandlerMiddleware>();
        }

        public static void UseCustomCors(
            this IApplicationBuilder builder)
        {
            builder.UseMiddleware<DevelopmentCorsMiddleware>();
        }

        public static void UseProductionCors(
            this IApplicationBuilder builder)
        {
            builder.UseMiddleware<ProductionCorsMiddleware>();
        }

        public static void UseFirewall(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<FirewallMiddleware>();
        }

        public static void UseSignature(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<SignatureMiddleware>();
        }

        public static void UseJwt(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<JwtMiddleware>();
        }

        public static void UseCustomGlobalRateLimiting(
            this IApplicationBuilder builder)
        {
            builder.UseMiddleware<CustomGlobalRateLimitingMiddleware>();
        }

        public static void UseCustomRateLimiting(
            this IApplicationBuilder builder)
        {
            builder.UseMiddleware<CustomRateLimitingMiddleware>();
        }

        public static void UseAntiXss(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<AntiXssMiddleware>();
        }
    }
}