using System.Runtime.InteropServices;

namespace PingMonitor;

public class MainForm : Form
{
    private readonly NotifyIcon trayIcon;
    private readonly Icon applicationIcon;
    private readonly System.Windows.Forms.Timer updateTimer;
    private readonly NetworkSpeedMonitor speedMonitor = new();
    private readonly StartupRegistration startup = new(Application.ExecutablePath);
    private readonly IPingProbe pingProbe;
    private Icon? currentIcon;
    private EnumColorType _currentColorType;
    private bool _isPaused;
    private bool updating;
    private bool disposed;
    private long? lastPing;
    private double downloadKbps;
    private double uploadKbps;
    private bool useBits = Properties.Settings.Default.UseBits;

    public MainForm() : this(new NetworkPingProbe()) { }

    internal MainForm(IPingProbe pingProbe)
    {
        this.pingProbe = pingProbe;
        using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("PingMonitor.AppIcon.ico")!)
        using (var sourceIcon = new Icon(stream, new Size(32, 32)))
            applicationIcon = (Icon)sourceIcon.Clone();
        Icon = applicationIcon;
        ShowInTaskbar = false;
        trayIcon = new NotifyIcon
        {
            Icon = applicationIcon,
            Visible = true,
            Text = "PingMonitor : mesure en cours…"
        };
        trayIcon.ContextMenuStrip = GetContextMenu();

        // Le timer Windows garde toutes les mises à jour de l'icône sur le thread UI.
        updateTimer = new System.Windows.Forms.Timer { Interval = 100 };
        updateTimer.Tick += async (_, _) =>
        {
            updateTimer.Interval = 2000;
            await UpdateNetworkInfoAsync();
        };
        updateTimer.Start();
    }

    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

    private ContextMenuStrip GetContextMenu()
    {
        var menu = new ContextMenuStrip();
        var pauseItem = new ToolStripMenuItem("Mettre en pause");
        pauseItem.Click += async (_, _) =>
        {
            _isPaused = !_isPaused;
            updateTimer.Enabled = !_isPaused;
            pauseItem.Text = _isPaused ? "Reprendre" : "Mettre en pause";
            if (_isPaused)
            {
                SetIcon(CreateTextIcon("Ⅱ", _currentColorType == EnumColorType.Background ? Color.DimGray : Color.Transparent, Color.White));
                trayIcon.Text = "PingMonitor : en pause";
            }
            else
            {
                speedMonitor.Reset();
                await UpdateNetworkInfoAsync();
            }
        };
        menu.Items.Add(pauseItem);

        var colors = new ToolStripMenuItem("Mode de couleur");
        var options = new Dictionary<ToolStripMenuItem, EnumColorType>
        {
            [new ToolStripMenuItem("Aucune")] = EnumColorType.No,
            [new ToolStripMenuItem("Couleur du texte")] = EnumColorType.Text,
            [new ToolStripMenuItem("Couleur de fond")] = EnumColorType.Background
        };
        int savedMode = Properties.Settings.Default.ColorMode;
        _currentColorType = Enum.IsDefined(typeof(EnumColorType), savedMode) ? (EnumColorType)savedMode : EnumColorType.No;
        foreach (var (item, mode) in options)
        {
            item.Checked = mode == _currentColorType;
            item.Click += (_, _) =>
            {
                foreach (var other in options.Keys) other.Checked = other == item;
                _currentColorType = mode;
                Properties.Settings.Default.ColorMode = (int)mode;
                Properties.Settings.Default.Save();
                if (!_isPaused) RenderMeasurements();
            };
            colors.DropDownItems.Add(item);
        }
        menu.Items.Add(colors);
        var units = new ToolStripMenuItem("Unité du débit");
        var bytesItem = new ToolStripMenuItem("Octets (ko/s, Mo/s)") { Checked = !useBits };
        var bitsItem = new ToolStripMenuItem("Bits (kb/s, Mb/s)") { Checked = useBits };
        void SelectUnits(bool bits)
        {
            useBits = bits;
            bytesItem.Checked = !bits;
            bitsItem.Checked = bits;
            Properties.Settings.Default.UseBits = bits;
            Properties.Settings.Default.Save();
            if (!_isPaused) RenderMeasurements();
        }
        bytesItem.Click += (_, _) => SelectUnits(false);
        bitsItem.Click += (_, _) => SelectUnits(true);
        units.DropDownItems.AddRange(new ToolStripItem[] { bytesItem, bitsItem });
        menu.Items.Add(units);
        var startupItem = new ToolStripMenuItem("Démarrer avec Windows") { Checked = startup.IsEnabled };
        startupItem.Click += (_, _) =>
        {
            try
            {
                startup.SetEnabled(!startup.IsEnabled);
                startupItem.Checked = startup.IsEnabled;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                MessageBox.Show("Impossible de modifier le démarrage automatique : " + ex.Message, "PingMonitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        menu.Opening += (_, _) => startupItem.Checked = startup.IsEnabled;
        menu.Items.Add(startupItem);
        menu.Items.Add("Quitter", null, (_, _) => Application.Exit());
        return menu;
    }

    private async Task UpdateNetworkInfoAsync()
    {
        if (_isPaused || updating || disposed) return;
        updating = true;
        try
        {
            // Une cible ICMP bloquée ne doit pas masquer une connexion fonctionnelle.
            var result = await pingProbe.MeasureAsync();
            if (_isPaused || disposed) return;
            lastPing = result.Ping;
            (downloadKbps, uploadKbps) = speedMonitor.Sample();
            RenderMeasurements();
        }
        catch (Exception)
        {
            if (!disposed && !_isPaused)
            {
                SetIcon(CreateTextIcon("!", _currentColorType == EnumColorType.Background ? Color.DarkRed : Color.Transparent, Color.White));
                trayIcon.Text = "PingMonitor : erreur de mesure";
            }
        }
        finally { updating = false; }
    }

    private void RenderMeasurements()
    {
        Color background = Color.Transparent;
        Color foreground = Color.White;
        if (_currentColorType == EnumColorType.Background)
        {
            background = lastPing switch { null => Color.DimGray, <= 50 => Color.ForestGreen, <= 100 => Color.Gold, <= 200 => Color.Orange, _ => Color.DarkRed };
            if (lastPing is > 50 and <= 200) foreground = Color.Black;
        }
        if (_currentColorType == EnumColorType.Text)
            foreground = lastPing switch { null => Color.LightGray, <= 50 => Color.LightGreen, <= 100 => Color.Gold, <= 200 => Color.Orange, _ => Color.Salmon };
        SetIcon(CreateTextIcon(lastPing?.ToString() ?? "N/A", background, foreground));
        string latency = lastPing.HasValue ? $"{lastPing} ms" : "N/A (pas de réponse ICMP)";
        string speeds = $"↓ {SpeedFormatter.Format(downloadKbps, useBits)} | ↑ {SpeedFormatter.Format(uploadKbps, useBits)}";
        trayIcon.Text = $"Ping : {latency}\n{speeds}";
    }

    private void SetIcon(Icon icon)
    {
        trayIcon.Icon = icon;
        currentIcon?.Dispose();
        currentIcon = icon;
    }

    private static Icon CreateTextIcon(string text, Color background, Color foreground)
    {
        const int iconSize = 16;
        using var bitmap = new Bitmap(iconSize, iconSize);
        using var mask = new Bitmap(iconSize, iconSize);
        const TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        float fontSize = 12;
        using (var graphics = Graphics.FromImage(mask))
        {
            graphics.Clear(Color.Black);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Font font = new Font("Segoe UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            try
            {
                while (fontSize > 7 && TextRenderer.MeasureText(graphics, text, font, Size.Empty, flags).Width > iconSize)
                {
                    font.Dispose();
                    font = new Font("Segoe UI", --fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
                }
                TextRenderer.DrawText(graphics, text, font, new Rectangle(0, 0, iconSize, iconSize), Color.White, Color.Black, flags);
            }
            finally { font.Dispose(); }
        }
        // Convertir le masque GDI en alpha pour conserver un vrai fond transparent.
        for (int y = 0; y < iconSize; y++)
            for (int x = 0; x < iconSize; x++)
            {
                Color pixel = mask.GetPixel(x, y);
                int coverage = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                if (background.A == 0)
                    bitmap.SetPixel(x, y, Color.FromArgb(coverage, foreground));
                else
                    bitmap.SetPixel(x, y, Color.FromArgb(
                        (foreground.R * coverage + background.R * (255 - coverage)) / 255,
                        (foreground.G * coverage + background.G * (255 - coverage)) / 255,
                        (foreground.B * coverage + background.B * (255 - coverage)) / 255));
            }
        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally { DestroyIcon(handle); }
    }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            updateTimer.Stop();
            updateTimer.Dispose();
            trayIcon.Visible = false;
            trayIcon.ContextMenuStrip?.Dispose();
            trayIcon.Dispose();
            currentIcon?.Dispose();
            applicationIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
