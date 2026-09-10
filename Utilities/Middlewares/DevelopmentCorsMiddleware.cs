using Microsoft.AspNetCore.Http;

namespace Utilities.Middlewares
{
    public class DevelopmentCorsMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext httpContext)
        {
            AddHeaders(httpContext);

            if (HttpMethods.IsOptions(httpContext.Request.Method))
            {
                httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            await next(httpContext);
        }

        private static void AddHeaders(HttpContext httpContext)
        {
            httpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";

            httpContext.Response.Headers["Access-Control-Allow-Headers"] =
                "Origin, X-Requested-With, Content-Type, Accept, Authorization, ApplicationId, Nonce, Signature, x-signalr-user-agent";

            httpContext.Response.Headers["Access-Control-Allow-Methods"] =
                "GET, POST, PUT, DELETE, OPTIONS";

            httpContext.Response.Headers["Vary"] = "Origin";

            httpContext.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self'; frame-src 'self'";

            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            httpContext.Response.Headers["X-Frame-Options"] = "DENY";
            httpContext.Response.Headers["X-XSS-Protection"] = "1; mode=block";

            httpContext.Response.Headers.Remove("Server");
        }
    }
}