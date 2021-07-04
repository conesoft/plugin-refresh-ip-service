using Conesoft.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Conesoft.Services.RefreshIP
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfigurationRoot configuration;
        private readonly File file;
        private readonly Ipify.Client ipify;
        private readonly DNSimple.Client dnsimple;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            configuration = new ConfigurationBuilder().AddJsonFile(Hosting.Host.GlobalConfiguration.Path).Build();
            file = Hosting.Host.GlobalStorage / "FromSources" / "Ipify" / Filename.From("Ip", "txt");
            file.Parent.Create();
            ipify = new Ipify.Client(new HttpClient());
            dnsimple = new DNSimple.Client(new HttpClient());
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                try
                {
                    var lastIp = file.Exists ? IPAddress.Parse(await file.ReadText()) : IPAddress.None;

                    var currentIp = await ipify.GetPublicIPAddress();

                    if (currentIp.ToString() != lastIp.ToString()) // urgh, value comparison, the cheap way
                    {
                        _logger.LogInformation("Updating IP Address to {new}", currentIp);
                        await UpdateDnsRecord(currentIp);
                        await file.WriteText(currentIp.ToString());
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                    _logger.LogError(e.ToString());
                }

                await Task.Delay(1000, stoppingToken);
            }
        }

        public async Task UpdateDnsRecord(IPAddress address)
        {
            dnsimple.UseToken(configuration["hosting:dnsimple-token"]);

            var account = (await dnsimple.GetAccounts()).First();

            var zones = await account.GetZones();

            foreach(var zone in zones)
            {

                var hosted = Hosting.Host.Root / "Deployments" / "Websites" / Filename.From(zone.Name, "zip");
                if(hosted.Exists)
                {
                    _logger.LogInformation("Updating Zone {zone}", zone.Name);
                    var record = await zone.GetRecord(DNSimple.RecordType.A);
                    await record.Update(address.ToString());
                }
            }
        }
    }
}
