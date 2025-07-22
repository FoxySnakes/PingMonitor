using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace PingMonitor
{
    public class MainForm : Form
    {
        private NotifyIcon trayIcon;
        private System.Timers.Timer updateTimer;

        private EnumColorType _currentColorType;
        private bool _isPaused;

        public MainForm()
        {
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Visible = true,
                Text = "Chargement..."
            };

            var contextMenu = GetContextMenu();
            // Appliquer au NotifyIcon
            trayIcon.ContextMenuStrip = contextMenu;

            // Timer : toutes les 5 secondes
            updateTimer = new System.Timers.Timer(5000);
            updateTimer.Elapsed += UpdateNetworkInfo;
            updateTimer.Start();
            UpdateNetworkInfo(this, null);

            // Cache la fenêtre principale
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        private ContextMenuStrip GetContextMenu()
        {
            // === Menu contextuel principal ===
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            // → 1. Rafraîchir manuellement
            contextMenu.Items.Add("Rafraîchir", null, (s, e) =>
            {
                UpdateNetworkInfo(this, null);
            });

            // → 2. Pause / Reprendre
            ToolStripMenuItem pauseItem = new ToolStripMenuItem("Mettre en pause");
            pauseItem.Click += (s, e) =>
            {
                _isPaused = !_isPaused;
                updateTimer.Enabled = !_isPaused;
                pauseItem.Text = _isPaused ? "Reprendre" : "Mettre en pause";

                if (_isPaused)
                {
                    trayIcon.Icon = CreatePauseIcon();
                    trayIcon.Text = "Pause activée – Ping en veille";
                }
                else
                    UpdateNetworkInfo(this, null);
            };
            contextMenu.Items.Add(pauseItem);

            // → 3. Enum de types de couleur
            ToolStripMenuItem colorTypeMenu = new ToolStripMenuItem("Mode de couleur");

            // Création des items radio
            ToolStripMenuItem noColor = new ToolStripMenuItem("Aucune") { CheckOnClick = true };
            ToolStripMenuItem textColor = new ToolStripMenuItem("Couleur du texte") { CheckOnClick = true };
            ToolStripMenuItem bgColor = new ToolStripMenuItem("Couleur de fond") { CheckOnClick = true };

            var colorItems = new Dictionary<ToolStripMenuItem, EnumColorType>
            {
                { noColor, EnumColorType.No },
                { textColor, EnumColorType.Text },
                { bgColor, EnumColorType.Background }
            };

            // Gère la sélection unique + met à jour le _currentColorType actuel
            foreach (var kvp in colorItems)
            {
                kvp.Key.Click += (s, e) =>
                {
                    foreach (var other in colorItems.Keys)
                        other.Checked = (other == s);

                    _currentColorType = colorItems[(ToolStripMenuItem)s];

                    // (optionnel) enregistrer en settings
                    Properties.Settings.Default.ColorMode = (int)_currentColorType;
                    Properties.Settings.Default.Save();

                    UpdateNetworkInfo(null, null); // Mise à jour immédiate
                };
            }

            // Charger l'état sauvegardé au démarrage
            if (Enum.IsDefined(typeof(EnumColorType), Properties.Settings.Default.ColorMode))
            {
                _currentColorType = (EnumColorType)Properties.Settings.Default.ColorMode;
                var selectedItem = colorItems.FirstOrDefault(kvp => kvp.Value == _currentColorType).Key;
                if (selectedItem != null) selectedItem.Checked = true;
            }

            // Ajouter au sous-menu
            colorTypeMenu.DropDownItems.AddRange(colorItems.Keys.ToArray());
            contextMenu.Items.Add(colorTypeMenu);

            // → 4. Quitter
            contextMenu.Items.Add("Quitter", null, (s, e) =>
            {
                if (MessageBox.Show("Quitter l'application ?", "Confirmation", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    Application.Exit();
            });

            return contextMenu;
        }

        private void UpdateNetworkInfo(object sender, ElapsedEventArgs e)
        {
            if (_isPaused) return;

            try
            {
                long? ping = GetPing("8.8.8.8");

                trayIcon.Icon = CreateTextIcon(ping);

                trayIcon.Text = $"Ping: {ping} ms\n↓ {GetSpeed("download")} Kbps | ↑ {GetSpeed("upload")} Kbps";

                string downSpeed = GetSpeed("download") + " Kbps";
                string upSpeed = GetSpeed("upload") + " Kbps";

                trayIcon.Text = $"Ping: {(ping == null ? "N/A" : ping.ToString())}\n↓ {downSpeed} | ↑ {upSpeed}";
            }
            catch
            {
                trayIcon.Text = "Erreur de mesure réseau.";
            }
        }

        private long? GetPing(string host)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(host, 1000); // 1 sec timeout
                    if (reply.Status == IPStatus.Success)
                        return reply.RoundtripTime;
                }
            }
            catch { }

            return null;
        }


        private string GetSpeed(string type)
        {
            return "0"; // TODO
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            trayIcon.Visible = false;
            base.OnFormClosing(e);
        }

        private Icon CreateTextIcon(long? ping)
        {
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                Color bgColor = Color.Transparent;
                Brush textBrush = Brushes.Black;

                if (ping == null)
                {
                    if (_currentColorType == EnumColorType.Background) bgColor = Color.Black;
                    if (_currentColorType == EnumColorType.Text) textBrush = Brushes.Gray;
                }
                else
                {
                    if (_currentColorType == EnumColorType.Background)
                    {
                        if (ping <= 50) bgColor = Color.ForestGreen;
                        else if (ping <= 100) bgColor = Color.Gold;
                        else if (ping <= 200) bgColor = Color.Orange;
                        else bgColor = Color.Red;
                    }

                    if (_currentColorType == EnumColorType.Text)
                    {
                        if (ping <= 50) textBrush = Brushes.Green;
                        else if (ping <= 100) textBrush = Brushes.Goldenrod;
                        else if (ping <= 200) textBrush = Brushes.Orange;
                        else textBrush = Brushes.Red;
                    }
                }

                g.Clear(bgColor);

                int fontSize = (ping == null || ping.ToString().Length > 2) ? 16 : 22;

                using (Font font = new Font("Segoe UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.DrawString(ping == null ? "N/A" : ping.ToString(), font, textBrush, new RectangleF(0, 0, 32, 32), format);
                }
            }

            IntPtr hIcon = bitmap.GetHicon();
            Icon icon = Icon.FromHandle(hIcon);
            return icon;
        }

        private Icon CreatePauseIcon()
        {
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.LightGray); // Fond neutre

                using (Pen pen = new Pen(Color.Black, 5))
                {
                    g.DrawLine(pen, 10, 8, 10, 24); // barre gauche
                    g.DrawLine(pen, 20, 8, 20, 24); // barre droite
                }
            }

            IntPtr hIcon = bitmap.GetHicon();
            return Icon.FromHandle(hIcon);
        }

    }
}
