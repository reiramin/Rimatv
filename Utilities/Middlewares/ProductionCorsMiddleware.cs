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
"https://api.rima.com",
"https://iptv-za7i.onrender.com"
};
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var origin = httpContext.Request.Headers.Origin.ToString();

        // Non-browser requests such as curl/Postman do not
        // necessarily contain an Origin header.
        if (string.IsNullOrWhiteSpace(origin))
        {
            await next(httpContext);
            return;
        }

        // Browser request: validate the Origin.
        if (!AllowedOrigins.Contains(origin))
        {
            httpContext.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            await httpContext.Response.WriteAsync("Access Denied");
            return;
        }

        AddCorsHeaders(httpContext, origin);

        // Handle CORS preflight.
        if (HttpMethods.IsOptions(httpContext.Request.Method))
        {
            httpContext.Response.StatusCode =
                StatusCodes.Status204NoContent;

            return;
        }

        await next(httpContext);
    }

    private static void AddCorsHeaders(
        HttpContext httpContext,
        string origin)
    {
        httpContext.Response.Headers["Access-Control-Allow-Origin"] =
            origin;

        httpContext.Response.Headers["Access-Control-Allow-Credentials"] =
            "true";

        httpContext.Response.Headers["Access-Control-Allow-Headers"] =
            "x-signalr-user-agent, Origin, X-Requested-With, Content-Type, Accept, Authorization, ApplicationId, Nonce, Signature";

        httpContext.Response.Headers["Access-Control-Allow-Methods"] =
            "GET, POST, PUT, DELETE, OPTIONS";

        httpContext.Response.Headers["Vary"] =
            "Origin";

        httpContext.Response.Headers["X-Content-Type-Options"] =
            "nosniff";

        httpContext.Response.Headers["X-Frame-Options"] =
            "DENY";

        httpContext.Response.Headers["X-XSS-Protection"] =
            "1; mode=block";

        httpContext.Response.Headers["Content-Security-Policy"] =
            "frame-ancestors 'self' https://rima.com https://mp.rima.com https://panel.rima.com https://api.rima.com";

        httpContext.Response.Headers.Remove("Server");
    }
}


}
