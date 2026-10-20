using System.Net.NetworkInformation;

namespace PingMonitor;

internal interface IPingProbe
{
    Task<(long? Ping, string Host)> MeasureAsync();
}

internal sealed class NetworkPingProbe : IPingProbe
{
    private string lastHost = "1.1.1.1";

    public async Task<(long? Ping, string Host)> MeasureAsync()
    {
        // Retenter d'abord la dernière cible qui a répondu évite des délais répétés.
        foreach (string host in new[] { lastHost, "1.1.1.1", "8.8.8.8" }.Distinct())
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, 1000);
                if (reply.Status != IPStatus.Success) continue;
                lastHost = host;
                return (reply.RoundtripTime, host);
            }
            catch (PingException) { }
        }
        return (null, "1.1.1.1 / 8.8.8.8");
    }
}
