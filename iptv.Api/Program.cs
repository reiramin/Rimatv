using Autofac;
using Autofac.Extensions.DependencyInjection;
using iptv.Api.Utilities.Configurations;
using Utilities.Configuration;
using Utilities.Models.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCustomControllers();

builder.Services.AddCustomApiVersioning();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwagger();

builder.Services.AddMemoryCache();

builder.Services.AddCoreSettings(builder.Configuration);
builder.Services.AddSettings(builder.Configuration);

// var proxySettings = builder.Configuration
//     .GetSection(nameof(OutboundProxySettings))
//     .Get<OutboundProxySettings>() ?? new OutboundProxySettings();


builder.Services.AddHttpClient("IptvProvider", client => { client.Timeout = TimeSpan.FromSeconds(90); });

// builder.Services.AddHttpClient("IptvProvider", client => { client.Timeout = TimeSpan.FromSeconds(15); });
//
// builder.Services.AddHttpClient("IptvProvider-Proxied", client => { client.Timeout = TimeSpan.FromSeconds(15); })
//     .ConfigurePrimaryHttpMessageHandler(() =>
//     {
//         var handler = new HttpClientHandler();
//         if (proxySettings.Enabled && !string.IsNullOrWhiteSpace(proxySettings.Address))
//         {
//             var proxy = new System.Net.WebProxy(proxySettings.Address);
//             if (!string.IsNullOrWhiteSpace(proxySettings.Username))
//                 proxy.Credentials = new System.Net.NetworkCredential(
//                     proxySettings.Username, proxySettings.Password);
//
//             handler.Proxy = proxy;
//             handler.UseProxy = true;
//         }
//
//         return handler;
//     });


builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(autofacConfigure =>
{
    autofacConfigure.AddCoreServices();
    autofacConfigure.AddControllerServices();
});


var app = builder.Build();

app.UseHsts(app.Environment);

app.UseDeveloperExceptionPage(app.Environment);

app.UseSwaggerAndUI();

app.UseLogger();

app.UseCustomExceptionHandler();

app.UseCustomCors();

app.UseFirewall();

app.UseSignature();

app.UseJwt();

app.UseRouting();

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