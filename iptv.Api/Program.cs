using Autofac;
using Autofac.Extensions.DependencyInjection;
using iptv.Api.Utilities.Configurations;
using Utilities.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCustomControllers();

builder.Services.AddCustomApiVersioning();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwagger();

builder.Services.AddMemoryCache();

// resolveUrl falls back to the request host when AppSettings:BaseUrl is unset (ResolveBase).
builder.Services.AddHttpContextAccessor();

// Outbound bandwidth on the free host is capped (5 GB/month): gzip every JSON response.
builder.Services.AddGzipResponseCompression();
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.ResponseCompressionOptions>(
    o => o.EnableForHttps = true);

// The validator posts probe results gzip-compressed (Content-Encoding: gzip).
builder.Services.AddRequestDecompression();

builder.Services.AddCoreSettings(builder.Configuration);
builder.Services.AddSettings(builder.Configuration);

builder.Services.AddHttpClient(
    "IptvProvider",
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(90);
    });

// Resolver fetches go through an SSRF guard (public addresses only, every redirect hop, max 5).
builder.Services.AddHttpClient(iptv.Services._Resolver.StreamResolverService.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(iptv.Services._Resolver.SsrfGuard.CreateHandler);

builder.Host.UseServiceProviderFactory(
    new AutofacServiceProviderFactory());

builder.Host.ConfigureContainer<ContainerBuilder>(
    autofacConfigure =>
    {
        autofacConfigure.AddCoreServices();
        autofacConfigure.AddControllerServices();
    });

var app = builder.Build();

// Early: before any middleware that reads the request body (logging, AntiXss) or writes responses.
// Decompression only for the signed probe endpoint (its action caps the body at 4 MB).
app.UseWhen(RequestDecompressionScope.Applies, branch => branch.UseRequestDecompression());
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseCustomCors();
}
else
{
    app.UseProductionCors();
}

app.UseHsts(app.Environment);

app.UseDeveloperExceptionPage(app.Environment);

app.UseSwaggerAndUI();

app.UseRouting();

app.UseCustomExceptionHandler();

app.UseLogger();

app.UseFirewall();

app.UseSignature();

app.UseJwt();

app.UseSecurityStamp();

app.UseAntiXss();

app.UseCustomRateLimiting();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapIptvHub();
});

app.Run();