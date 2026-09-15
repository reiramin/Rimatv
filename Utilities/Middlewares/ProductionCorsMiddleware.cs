using Microsoft.AspNetCore.Http;

namespace Utilities.Middlewares
{
    public class ProductionCorsMiddleware(RequestDelegate next)
    {
        private static readonly HashSet<string> ProductionAllowedOrigins =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "https://rimatv.github.io",
                "https://rima.com",
                "https://mp.rima.com",
                "https://panel.rima.com",
                "https://api.rima.com"
            };

        public async Task InvokeAsync(HttpContext httpContext)
        {
            var origin = httpContext.Request.Headers.Origin.ToString();

            if (!string.IsNullOrWhiteSpace(origin))
            {
                if (!IsAllowedOrigin(origin))
                {
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await httpContext.Response.WriteAsync("Access Denied");
                    return;
                }

                AddCorsHeaders(httpContext, origin);
            }

            if (HttpMethods.IsOptions(httpContext.Request.Method))
            {
                httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            await next(httpContext);
        }

        private static bool IsAllowedOrigin(string origin)
        {
            if (origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(origin, "http://localhost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(origin, "http://127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return ProductionAllowedOrigins.Contains(origin);
        }

        private static void AddCorsHeaders(
            HttpContext httpContext,
            string origin)
        {
            httpContext.Response.Headers["Access-Control-Allow-Origin"] = origin;
            httpContext.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            httpContext.Response.Headers["Vary"] = "Origin";

            httpContext.Response.Headers["Access-Control-Allow-Headers"] =
                "x-signalr-user-agent, Origin, X-Requested-With, Content-Type, Accept, Authorization, ApplicationId, Nonce, Signature";

            httpContext.Response.Headers["Access-Control-Allow-Methods"] =
                "GET, POST, PUT, DELETE, OPTIONS";

            httpContext.Response.Headers["Access-Control-Max-Age"] = "86400";

            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            httpContext.Response.Headers["X-Frame-Options"] = "DENY";
            httpContext.Response.Headers["X-XSS-Protection"] = "1; mode=block";

            httpContext.Response.Headers["Content-Security-Policy"] =
                "frame-ancestors 'self' https://rimatv.github.io https://rima.com https://mp.rima.com https://panel.rima.com https://api.rima.com";

            httpContext.Response.Headers.Remove("Server");
        }
    }
}