namespace PingMonitor;

internal static class SpeedFormatter
{
    // Le mesureur fournit des kilobits/s. Préfixes décimaux : 1 ko = 1000 octets.
    public static string Format(double kilobitsPerSecond, bool useBits)
    {
        double kilo = Math.Max(0, kilobitsPerSecond) / (useBits ? 1 : 8);
        return kilo >= 100
            ? $"{kilo / 1000:F2} {(useBits ? "Mb/s" : "Mo/s")}"
            : $"{kilo:F1} {(useBits ? "kb/s" : "ko/s")}";
    }
}
