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

builder.Services.AddCoreSettings(builder.Configuration);
builder.Services.AddSettings(builder.Configuration);

builder.Services.AddHttpClient(
    "IptvProvider",
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(90);
    });

builder.Host.UseServiceProviderFactory(
    new AutofacServiceProviderFactory());

builder.Host.ConfigureContainer<ContainerBuilder>(
    autofacConfigure =>
    {
        autofacConfigure.AddCoreServices();
        autofacConfigure.AddControllerServices();
    });

var app = builder.Build();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerAndUI();
}

app.UseCustomExceptionHandler();

app.UseLogger();

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