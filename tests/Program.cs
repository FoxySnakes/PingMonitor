using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using PingMonitor;

internal static class Program
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }

    [DllImport("user32.dll")]
    private static extern int GetGuiResources(IntPtr process, int flag);

    [STAThread]
    private static int Main(string[] args)
    {
        bool realNetwork = args.Contains("--network");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        CheckStartup();
        Control.CheckForIllegalCrossThreadCalls = true;
        var fakeProbe = new FixedPingProbe();
        using var form = realNetwork ? new MainForm() : new MainForm(fakeProbe);
        using var context = new ApplicationContext();
        using var starter = new System.Windows.Forms.Timer { Interval = 100 };
        int exitCode = 0;
        starter.Tick += async (_, _) =>
        {
            starter.Stop();
            var settingsType = typeof(MainForm).Assembly.GetType("PingMonitor.Properties.Settings")!;
            var settings = settingsType.GetProperty("Default")!.GetValue(null)!;
            var modeProperty = settingsType.GetProperty("ColorMode")!;
            int originalMode = (int)modeProperty.GetValue(settings)!;
            var unitsProperty = settingsType.GetProperty("UseBits")!;
            bool originalUnits = (bool)unitsProperty.GetValue(settings)!;
            try
            {
                var tray = Field<NotifyIcon>(form, "trayIcon");
                await Task.Delay(4000);
                var units = (ToolStripMenuItem)tray.ContextMenuStrip!.Items[2];
                ((ToolStripMenuItem)units.DropDownItems[0]).PerformClick();
                Check(Field<long?>(form, "lastPing").HasValue, realNetwork ? "Réponse ICMP réelle et rafraîchissement automatique" : "Rafraîchissement automatique avec sonde simulée");
                Check(tray.Text.Contains("ms") && (tray.Text.Contains("ko/s") || tray.Text.Contains("Mo/s")), "Infobulle avec latence et débits en octets");
                var formatter = typeof(MainForm).Assembly.GetType("PingMonitor.SpeedFormatter")!.GetMethod("Format")!;
                string Format(double value, bool bits) => (string)formatter.Invoke(null, new object[] { value, bits })!;
                Check(Format(0, false) == $"{0:F1} ko/s", "Zéro en ko/s");
                Check(Format(400, false) == $"{50:F1} ko/s", "Conversion bits vers octets (division par 8)");
                Check(Format(799.9, false).EndsWith("ko/s"), "Sous 0,1 Mo/s : ko/s");
                Check(Format(800, false) == $"{0.1:F2} Mo/s", "À 0,1 Mo/s : passage en Mo/s");
                Check(Format(8000, false) == $"{1:F2} Mo/s", "Conversion vers Mo/s");
                Check(Format(99.9, true).EndsWith("kb/s") && Format(100, true) == $"{0.1:F2} Mb/s", "Unités automatiques en bits");
                ((ToolStripMenuItem)units.DropDownItems[1]).PerformClick();
                Check((tray.Text.Contains("kb/s") || tray.Text.Contains("Mb/s")) &&
                    ((ToolStripMenuItem)units.DropDownItems[1]).Checked && !((ToolStripMenuItem)units.DropDownItems[0]).Checked,
                    "Changement immédiat vers les bits");
                settingsType.GetMethod("Reload")!.Invoke(settings, null);
                Check((bool)unitsProperty.GetValue(settings)!, "Choix des unités sauvegardé");
                ((ToolStripMenuItem)units.DropDownItems[0]).PerformClick();
                Console.WriteLine(tray.Text);
                Check(!form.Visible && !form.ShowInTaskbar, "Aucune fenêtre vide ni bouton dans la barre des tâches");

                if (!realNetwork)
                {
                    fakeProbe.Latency = null;
                    await Task.Delay(2400);
                    Check(tray.Text.Contains("N/A") && !tray.Text.Contains("erreur"), "Absence de réponse ICMP affichée sans erreur");
                    fakeProbe.Latency = 5;
                    await Task.Delay(2400);
                    Check(tray.Text.Contains("5 ms"), "Récupération après le retour des réponses ICMP");
                }

                if (realNetwork)
                {
                    var sampler = Field<object>(form, "speedMonitor");
                    var samplerType = sampler.GetType();
                    samplerType.GetMethod("Reset")!.Invoke(sampler, null);
                    using (var ping = new Ping())
                        for (int i = 0; i < 5; i++) await ping.SendPingAsync("1.1.1.1", 1000);
                    var rates = ((double, double))samplerType.GetMethod("Sample")!.Invoke(sampler, null)!;
                    Check(rates.Item1 > 0 && rates.Item2 > 0, $"Trafic réel mesuré : réception {rates.Item1:F1}, émission {rates.Item2:F1} kbit/s");
                }

                var pause = (ToolStripMenuItem)tray.ContextMenuStrip!.Items[0];
                pause.PerformClick();
                Check(tray.Text.Contains("pause") && !Field<System.Windows.Forms.Timer>(form, "updateTimer").Enabled, "Pause des mesures");
                string pausedText = tray.Text;
                await Task.Delay(2400);
                Check(tray.Text == pausedText, "La pause reste active après un intervalle");
                pause.PerformClick();
                await Task.Delay(2400);
                Check(tray.Text.Contains("ms") && Field<System.Windows.Forms.Timer>(form, "updateTimer").Enabled, "Reprise des mesures");

                var colors = (ToolStripMenuItem)tray.ContextMenuStrip.Items[1];
                foreach (ToolStripMenuItem option in colors.DropDownItems)
                {
                    option.PerformClick();
                    Check(colors.DropDownItems.Cast<ToolStripMenuItem>().Count(x => x.Checked) == 1 && option.Checked, "Mode couleur : " + option.Text);
                }
                var render = typeof(MainForm).GetMethod("RenderMeasurements", Private)!;
                // Stabiliser le cache de polices GDI de TextRenderer avant de mesurer.
                for (int i = 0; i < 100; i++) render.Invoke(form, null);
                int before = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                int userBefore = GetGuiResources(Process.GetCurrentProcess().Handle, 1);
                for (int i = 0; i < 500; i++) render.Invoke(form, null);
                int after = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
                int userAfter = GetGuiResources(Process.GetCurrentProcess().Handle, 1);
                Check(userAfter - userBefore < 8, $"500 actualisations sans fuite USER (variation {userAfter - userBefore})");
                Check(after - before < 8, $"500 actualisations sans fuite GDI (variation {after - before})");
                form.Dispose();
                Check(!tray.Visible, "Nettoyage de l’icône à la fermeture");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                exitCode = 1;
            }
            finally
            {
                modeProperty.SetValue(settings, originalMode);
                unitsProperty.SetValue(settings, originalUnits);
                settingsType.GetMethod("Save")!.Invoke(settings, null);
                context.ExitThread();
            }
        };
        starter.Start();
        Application.Run(context);
        return exitCode;
    }

    private sealed class FixedPingProbe : IPingProbe
    {
        public long? Latency { get; set; } = 5;
        public Task<(long? Ping, string Host)> MeasureAsync() => Task.FromResult<(long?, string)>((Latency, "test"));
    }

    private static void CheckStartup()
    {
        string key = @"Software\PingMonitor\Tests\" + Guid.NewGuid();
        try
        {
            var startup = new StartupRegistration(@"C:\Program Files\PingMonitor\PingMonitor.exe", key);
            startup.SetEnabled(true);
            Check(startup.IsEnabled, "Activation du démarrage automatique (clé de test isolée)");
            using (var testKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key))
                Check((string?)testKey?.GetValue("PingMonitor") == "\"C:\\Program Files\\PingMonitor\\PingMonitor.exe\"", "Chemin de démarrage correctement protégé par des guillemets");
            startup.SetEnabled(false);
            Check(!startup.IsEnabled, "Désactivation du démarrage automatique");
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(key, false);
        }
    }
}
