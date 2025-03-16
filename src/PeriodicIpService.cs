using Conesoft.Files;
using Conesoft.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Conesoft.Services.RefreshIP;

public class PeriodicIpService(HostEnvironment environment, Ipify.Client ipify, DNSimple.Client dnsimple, IOptions<DnsimpleConfiguration> dnsimpleConfiguration) : PeriodicTask(TimeSpan.FromSeconds(1))
{
    protected override async Task Process()
    {
        try
        {
            var file = environment.Global.Storage / "host" / Filename.From("ip", "txt");
            var lastIp = file.Exists ? IPAddress.Parse(await file.ReadText()) : IPAddress.None;

            var currentIp = await ipify.GetPublicIPAddress();

            if (currentIp.ToString() != lastIp.ToString()) // urgh, value comparison, the cheap way
            {
                Log.Information("updating IP Address to {new}", currentIp);
                await UpdateDnsRecord(currentIp);
                await file.WriteText(currentIp.ToString());
            }
        }
        catch (Exception e)
        {
            Log.Error("error: {exception}", e.Message);
        }
    }

    async Task UpdateDnsRecord(IPAddress address)
    {
        dnsimple.UseToken(dnsimpleConfiguration.Value.Token);

        var account = (await dnsimple.GetAccounts()).First();

        var zones = await account.GetZones();

        foreach (var zone in zones)
        {

            var hosted = environment.Root / "Deployments" / "Websites" / Filename.From(zone.Name, "zip");
            if (hosted.Exists)
            {
                Log.Information("Updating Zone {zone}", zone.Name);
                var record = await zone.GetRecord(DNSimple.RecordType.A);
                await record.Update(address.ToString());
            }
        }
    }
}
