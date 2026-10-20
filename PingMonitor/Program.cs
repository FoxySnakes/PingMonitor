namespace PingMonitor;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var instance = new Mutex(true, @"Local\PingMonitor", out bool firstInstance);
        if (!firstInstance) return;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var form = new MainForm();
        Application.Run(form);
    }
}
