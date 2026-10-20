using System.Diagnostics;
using System.Net.NetworkInformation;

namespace PingMonitor;

internal sealed class NetworkSpeedMonitor
{
    private Dictionary<string, (long Received, long Sent)> previous = new();
    private long timestamp;

    public NetworkSpeedMonitor() => Reset();

    public void Reset()
    {
        previous = ReadCounters();
        timestamp = Stopwatch.GetTimestamp();
    }

    public (double DownloadKbps, double UploadKbps) Sample()
    {
        var counters = ReadCounters();
        long now = Stopwatch.GetTimestamp();
        double seconds = Stopwatch.GetElapsedTime(timestamp, now).TotalSeconds;
        long received = 0, sent = 0;
        foreach (var (id, current) in counters)
        {
            if (!previous.TryGetValue(id, out var old)) continue;
            received += Math.Max(0, current.Received - old.Received);
            sent += Math.Max(0, current.Sent - old.Sent);
        }
        previous = counters;
        timestamp = now;
        return seconds > 0 ? (received * 8d / 1000 / seconds, sent * 8d / 1000 / seconds) : (0, 0);
    }

    private static Dictionary<string, (long Received, long Sent)> ReadCounters()
    {
        var counters = new Dictionary<string, (long Received, long Sent)>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;
                // Une carte peut disparaître pendant l'énumération (Wi-Fi, VPN, etc.).
                if (adapter.GetIPProperties().GatewayAddresses.Count == 0) continue;
                var stats = adapter.GetIPStatistics();
                counters[adapter.Id] = (stats.BytesReceived, stats.BytesSent);
            }
            catch (NetworkInformationException) { }
        }
        return counters;
    }
}
