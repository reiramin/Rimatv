using Microsoft.AspNetCore.Http;

namespace Utilities.Middlewares
{
    public class ProductionCorsMiddleware(RequestDelegate next)
    {
        private static readonly HashSet<string> AllowedOrigins =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "https://rima.com",
                "https://mp.rima.com",
                "https://panel.rima.com",
                "https://api.rima.com"
            };

        public async Task InvokeAsync(HttpContext httpContext)
        {
            var origin = httpContext.Request.Headers.Origin.ToString();

            // Non-browser requests (curl, Postman, server-to-server)
            // do not necessarily send an Origin header.
            if (string.IsNullOrWhiteSpace(origin))
            {
                if (HttpMethods.IsOptions(httpContext.Request.Method))
                {
                    httpContext.Response.StatusCode =
                        StatusCodes.Status204NoContent;

                    return;
                }

                await next(httpContext);
                return;
            }

            // Browser request: validate origin.
            if (!AllowedOrigins.Contains(origin))
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                await httpContext.Response.WriteAsync("Access Denied");
                return;
            }

            httpContext.Response.Headers["Access-Control-Allow-Origin"] =
                origin;

            httpContext.Response.Headers["Access-Control-Allow-Credentials"] =
                "true";

            httpContext.Response.Headers["Vary"] =
                "Origin";

            httpContext.Response.Headers["Access-Control-Allow-Headers"] =
                "x-signalr-user-agent, Origin, X-Requested-With, Content-Type, Accept, Authorization, ApplicationId, Nonce, Signature";

            httpContext.Response.Headers["Access-Control-Allow-Methods"] =
                "GET, POST, PUT, DELETE, OPTIONS";

            httpContext.Response.Headers["X-Content-Type-Options"] =
                "nosniff";

            httpContext.Response.Headers["X-Frame-Options"] =
                "DENY";

            httpContext.Response.Headers["X-XSS-Protection"] =
                "1; mode=block";

            httpContext.Response.Headers["Content-Security-Policy"] =
                "frame-ancestors 'self' https://rima.com https://mp.rima.com https://panel.rima.com https://api.rima.com";

            httpContext.Response.Headers.Remove("Server");

            if (HttpMethods.IsOptions(httpContext.Request.Method))
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status204NoContent;

                return;
            }

            await next(httpContext);
        }
    }
}
