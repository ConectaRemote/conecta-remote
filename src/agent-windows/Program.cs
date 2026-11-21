using System.Windows.Forms;

namespace ConectaRemoteAgent;

/// <summary>
/// Ponto de entrada do ConectaRemote Portable.
/// Um único executável: a janela do app (WebView2) e o servidor local de controle
/// rodam no mesmo processo, sem instalação.
/// </summary>
internal static class Program
{
    private const string Version = "1.0.0";

    private static AgentServer? _server;
    private static NotifyIcon? _tray;
    private static HotkeyWindow? _hotkeyWindow;
    private static MainWindow? _window;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var config = AgentConfig.Load();
        var deviceName = Environment.MachineName;

        _server = new AgentServer(config, deviceName, Version);
        if (!_server.Start())
        {
            MessageBox.Show(
                "O ConectaRemote Portable já está aberto neste computador, ou a porta local 8765 está em uso.\n\n" +
                "Feche a outra janela do aplicativo e tente novamente.",
                "ConectaRemote Portable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _server.StatusChanged += SetStatus;
        SetupTray();
        SetupHotkey();

        SetStatus("Agente pronto", "Aguardando pareamento do navegador.");

        _window = new MainWindow(config.AppUrl);
        Application.Run(_window);

        _hotkeyWindow?.Dispose();
        _tray?.Dispose();
        _server?.Dispose();
    }

    private static void SetupTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir ConectaRemote", null, (_, _) => ShowWindow());
        menu.Items.Add("Interromper controle (Ctrl+Alt+Shift+F12)", null, (_, _) => _server?.EmergencyStop("tray"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair do ConectaRemote", null, (_, _) => Application.Exit());

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Visible = true,
            Text = "ConectaRemote Portable",
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowWindow();
    }

    private static void ShowWindow()
    {
        if (_window == null) return;
        if (_window.WindowState == FormWindowState.Minimized) _window.WindowState = FormWindowState.Normal;
        _window.Show();
        _window.Activate();
    }

    private static void SetupHotkey()
    {
        _hotkeyWindow = new HotkeyWindow(() => _server?.EmergencyStop("hotkey"));
        _ = _hotkeyWindow.Handle; // força a criação da janela que recebe o atalho global
    }

    private static void SetStatus(string title, string detail)
    {
        if (_tray == null) return;

        _tray.Text = $"ConectaRemote · {title}";
        _tray.Icon = _server?.IsControlling == true ? SystemIcons.Warning : SystemIcons.Shield;

        if (title.StartsWith("Controle", StringComparison.Ordinal) ||
            title.StartsWith("Agente", StringComparison.Ordinal))
        {
            _tray.BalloonTipTitle = title;
            _tray.BalloonTipText = detail;
            _tray.ShowBalloonTip(3000);
        }
    }
}