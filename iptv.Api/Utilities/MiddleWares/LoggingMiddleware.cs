using System.Text;
using System.Text.Json;
using iptv.Services._Log;
using iptv.Services._Log.DTOs.Updates;
using Utilities.Attributes;
using Utilities.Constants;
using Utilities.Extensions;

namespace iptv.Api.Utilities.MiddleWares
{
    public class LoggingMiddleware(RequestDelegate _next, ILogService _logService)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/health",
                    StringComparison.OrdinalIgnoreCase) || HttpMethods.IsOptions(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method))
            {
                await _next(context);
                return;
            }

            var endpoint = context.GetEndpoint();
            var ignoreLog = endpoint?.Metadata.GetMetadata<IgnoreLoggingAttribute>();
            if (ignoreLog != null)
            {
                await _next(context);
                return;
            }

            var request = context.Request;

            var headers = JsonSerializer.Serialize(
                request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));

            var query = request.QueryString.HasValue ? request.QueryString.Value : null;
            var path = request.Path.Value;
            var segments = path?.Split("/", StringSplitOptions.RemoveEmptyEntries);
            var ip = context.GetRequestIpv4()?.Split(",")[0];

            var user = CurrentRequestContext.User;
            var publicKey = user?.PublicKey ?? "anonymous";
            var phone = "anonymous";

            var log = new RequestLogUpdate
            {
                ControllerName = (segments != null && segments.Length > 2) ? segments[2] : null,
                ApiName = (segments != null && segments.Length > 3) ? segments[3] : null,
                Headers = headers,
                Query = string.IsNullOrWhiteSpace(query) ? null : query,
                RoutePath = path,
                ClientIP = ip
            };


            bool isMultipart = request.ContentType?.StartsWith("multipart/form-data") == true;
            bool hasBody = request.ContentLength.HasValue && request.ContentLength > 0;

            // Suspicious: Upload endpoint but not multipart
            if (path.Contains("/upload", StringComparison.OrdinalIgnoreCase) && hasBody && !isMultipart)
            {
                log.Body = "[⚠️ Suspicious] Non-multipart upload attempt";
                await _logService.CaptureRequestLogAsync(log, publicKey, phone);

                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                await context.Response.WriteAsync("Only multipart/form-data is allowed.");
                return;
            }

            // Suspicious: Huge query string (possible abuse)
            if (query != null && query.Length > 2048)
            {
                log.Body = "[⚠️ Suspicious] Query string too large";
                await _logService.CaptureRequestLogAsync(log, publicKey, phone);

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Query too large.");
                return;
            }

            // Suspicious: Large headers (possible header abuse)
            if (request.Headers.Sum(h => h.Key.Length + h.Value.ToString().Length) > 32 * 1024)
            {
                log.Body = "[⚠️ Suspicious] Headers too large";
                await _logService.CaptureRequestLogAsync(log, publicKey, phone);

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Headers too large.");
                return;
            }

            try
            {
                if (isMultipart)
                {
                    log.Body = JsonSerializer.Serialize(new
                    {
                        Type = "FileUpload",
                        Note = "Multipart form data"
                    });
                }
                else if (hasBody && request.ContentLength < 1024 * 1024 * 2) // < 1MB
                {
                    request.EnableBuffering();

                    using var reader = new StreamReader(
                        request.Body,
                        Encoding.UTF8,
                        detectEncodingFromByteOrderMarks: false,
                        bufferSize: 1024,
                        leaveOpen: true);

                    request.Body.Position = 0;
                    log.Body = await reader.ReadToEndAsync();
                    request.Body.Position = 0;
                }
                else if (hasBody)
                {
                    log.Body = "[Body skipped due to size]";
                }
            }
            catch
            {
                log.Body = "[Body read error]";
            }

            await _logService.CaptureRequestLogAsync(log, publicKey, phone);

            await _next(context);

        }
    }
}
