using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ConectaRemoteAgent;

/// <summary>
/// Janela única do ConectaRemote Portable: carrega o mesmo app publicado, com a mesma
/// conta, os mesmos códigos e as mesmas sessões da versão Web.
/// </summary>
internal sealed class MainWindow : Form
{
    private const string RuntimeDownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    /// <summary>
    /// Provedores que recusam login dentro de navegador embarcado. A navegação é
    /// interrompida aqui e a página de login abre no navegador padrão.
    /// </summary>
    private static readonly string[] ProviderHosts =
    {
        "accounts.google.com",
        "accounts.youtube.com",
        "login.microsoftonline.com",
        "login.live.com",
        "appleid.apple.com",
        "www.facebook.com",
        "m.facebook.com",
    };

    private readonly WebView2 _web = new();
    private readonly string _startUrl;
    private readonly string _loginUrl;

    private Panel? _fallbackPanel;
    private Label? _fallbackTitle;
    private Label? _fallbackText;
    private Button? _fallbackAction;
    private Button? _fallbackSecondary;
    private Action? _fallbackActionHandler;
    private Action? _fallbackSecondaryHandler;

    public MainWindow(string startUrl)
    {
        _startUrl = startUrl;
        _loginUrl = BuildLoginUrl(startUrl);

        Text = "ConectaRemote Portable";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 640);
        Size = new Size(1280, 820);
        Icon = SystemIcons.Shield;
        BackColor = Color.FromArgb(4, 13, 26);

        _web.Dock = DockStyle.Fill;
        Controls.Add(_web);

        BuildFallbackPanel();
    }

    private static string BuildLoginUrl(string startUrl)
    {
        if (Uri.TryCreate(startUrl, UriKind.Absolute, out var uri))
        {
            return new Uri(uri, "/login").ToString();
        }

        return startUrl;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ConectaRemotePortable", "WebView2");
            Directory.CreateDirectory(dataFolder);

            var environment = await CoreWebView2Environment.CreateAsync(null, dataFolder);
            await _web.EnsureCoreWebView2Async(environment);

            var core = _web.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.NewWindowRequested += OnNewWindowRequested;
            core.NavigationStarting += OnNavigationStarting;
            core.NavigationCompleted += OnNavigationCompleted;

            ShowFallback(false);
            _web.Source = new Uri(_startUrl);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowFallback(
                "Falta o componente de navegação do Windows",
                "Este computador não tem o Microsoft Edge WebView2 Runtime.\nInstale o componente e abra o ConectaRemote Portable novamente.",
                "Baixar o componente",
                () => OpenExternal(RuntimeDownloadUrl));
        }
        catch (Exception)
        {
            ShowFallback(
                "Não foi possível abrir o ConectaRemote",
                "A janela do aplicativo não conseguiu iniciar.\nVerifique a conexão com a internet e tente novamente.",
                "Tentar novamente",
                () => _ = InitializeAsync());
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) return;
        if (!IsProviderHost(uri.Host)) return;

        // O provedor recusa navegador embarcado: a sessão ficaria presa no navegador externo,
        // então a janela explica o caminho que funciona e mantém o usuário no app.
        e.Cancel = true;
        OpenExternal(uri.ToString());

        ShowFallback(
            "Use e-mail e senha neste aplicativo",
            "O Google, a Microsoft e a Apple recusam o login dentro de janelas de aplicativo como esta.\n\n" +
            "A página de login foi aberta no seu navegador, onde esses provedores funcionam, mas a sessão " +
            "fica lá e não é transferida para cá.\n\n" +
            "Para usar o ConectaRemote Portable, entre com e-mail e senha na tela do aplicativo. " +
            "Se a conta foi criada pelo Google, defina uma senha em \"Esqueci minha senha\" no site e use " +
            "esse e-mail e senha aqui.",
            "Entrar com e-mail e senha",
            () =>
            {
                ShowFallback(false);
                _web.Source = new Uri(_loginUrl);
            },
            "Abrir o site no navegador",
            () => OpenExternal(_loginUrl));
    }

    private static bool IsProviderHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;

        foreach (var provider in ProviderHosts)
        {
            if (host.Equals(provider, StringComparison.OrdinalIgnoreCase)) return true;
            if (host.EndsWith("." + provider, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        OpenExternal(e.Uri);
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess) return;
        ShowFallback(
            "Sem conexão com o ConectaRemote",
            "Não foi possível carregar o app.\nConfira a conexão com a internet e tente novamente.",
            "Tentar novamente",
            () => _web.Source = new Uri(_startUrl));
    }

    private static void OpenExternal(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return;

        try
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
        catch
        {
            // navegador padrão indisponível
        }
    }

    private void BuildFallbackPanel()
    {
        _fallbackTitle = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Font = new Font("Segoe UI Semibold", 16F),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 0, 0, 12),
        };

        _fallbackText = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(198, 208, 224),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 0, 0, 22),
        };

        _fallbackAction = CreateButton("Continuar", primary: true);
        _fallbackAction.Click += (_, _) => _fallbackActionHandler?.Invoke();

        _fallbackSecondary = CreateButton("Abrir no navegador", primary: false);
        _fallbackSecondary.Visible = false;
        _fallbackSecondary.Click += (_, _) => _fallbackSecondaryHandler?.Invoke();

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0),
        };
        buttons.Controls.Add(_fallbackAction);
        buttons.Controls.Add(_fallbackSecondary);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Anchor = AnchorStyles.None,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_fallbackTitle, 0, 0);
        layout.Controls.Add(_fallbackText, 0, 1);
        layout.Controls.Add(buttons, 0, 2);

        _fallbackPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 13, 26) };
        _fallbackPanel.Controls.Add(layout);
        _fallbackPanel.Resize += (_, _) =>
        {
            layout.Left = Math.Max(0, (_fallbackPanel.Width - layout.Width) / 2);
            layout.Top = Math.Max(0, (_fallbackPanel.Height - layout.Height) / 2);
        };

        Controls.Add(_fallbackPanel);
        _fallbackPanel.BringToFront();
        ShowFallback(false);
    }

    private static Button CreateButton(string text, bool primary)
    {
        var button = new Button
        {
            AutoSize = true,
            Padding = new Padding(22, 9, 22, 9),
            BackColor = primary ? Color.FromArgb(47, 107, 255) : Color.FromArgb(11, 32, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10F),
            Text = text,
            Margin = new Padding(4),
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void ShowFallback(bool visible)
    {
        if (_fallbackPanel == null) return;
        _fallbackPanel.Visible = visible;
        _web.Visible = !visible;
        if (visible) _fallbackPanel.BringToFront();
    }

    private void ShowFallback(string title, string text, string actionLabel, Action action)
    {
        ShowFallback(title, text, actionLabel, action, null, null);
    }

    private void ShowFallback(
        string title,
        string text,
        string actionLabel,
        Action action,
        string? secondaryLabel,
        Action? secondaryAction)
    {
        if (_fallbackPanel == null) return;
        if (_fallbackTitle != null) _fallbackTitle.Text = title;
        if (_fallbackText != null) _fallbackText.Text = text;
        if (_fallbackAction != null)
        {
            _fallbackAction.Text = actionLabel;
            _fallbackAction.Visible = true;
        }

        _fallbackActionHandler = action;

        if (_fallbackSecondary != null)
        {
            _fallbackSecondary.Visible = !string.IsNullOrWhiteSpace(secondaryLabel);
            if (_fallbackSecondary.Visible) _fallbackSecondary.Text = secondaryLabel!;
        }

        _fallbackSecondaryHandler = secondaryAction;
        ShowFallback(true);
    }
}