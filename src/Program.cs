using Conesoft.Hosting;
using Conesoft.Services.RefreshIP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder
    .AddHostConfigurationFiles(configurator =>
    {
        configurator.Add<DnsimpleConfiguration>("dnsimple");
    })
    .AddHostEnvironmentInfo()
    .AddLoggingService()
    ;

builder.Services
    .AddSingleton<Conesoft.Ipify.Client>()
    .AddSingleton<Conesoft.DNSimple.Client>()
    .AddHttpClient()
    .AddHostedService<PeriodicIpService>()
    ;

var host = builder.Build();
await host.RunAsync();