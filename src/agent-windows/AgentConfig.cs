using System.Text.Json;

namespace ConectaRemoteAgent;

internal sealed class AgentConfig
{
    /// <summary>Porta do servidor local. Somente loopback.</summary>
    public int Port { get; init; } = 8765;

    /// <summary>Endereço publicado do ConectaRemote, usado no pareamento e na janela do app.</summary>
    public string AppBaseUrl { get; init; } = "https://straight-conecta-remote-link.base44.app";

    /// <summary>Página aberta na janela do aplicativo portátil.</summary>
    public string AppUrl { get; init; } = "https://straight-conecta-remote-link.base44.app/login";

    public string[] AllowedOrigins { get; init; } = Array.Empty<string>();

    public bool AllowBase44Subdomains { get; init; } = true;

    public bool AllowLocalOrigins { get; init; } = true;

    public static AgentConfig Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var config = new AgentConfig();

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                config = JsonSerializer.Deserialize<AgentConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                }) ?? config;
            }
            catch
            {
                // configuração inválida: seguem os valores padrão
            }
        }

        // A origem do próprio app é sempre aceita, mesmo que a lista venha incompleta.
        var origins = new List<string>(config.AllowedOrigins ?? Array.Empty<string>());
        if (Uri.TryCreate(config.AppBaseUrl, UriKind.Absolute, out var appUri))
        {
            var origin = appUri.GetLeftPart(UriPartial.Authority);
            if (!origins.Contains(origin, StringComparer.OrdinalIgnoreCase)) origins.Add(origin);
        }

        return new AgentConfig
        {
            Port = config.Port,
            AppBaseUrl = config.AppBaseUrl,
            AppUrl = string.IsNullOrWhiteSpace(config.AppUrl) ? config.AppBaseUrl : config.AppUrl,
            AllowedOrigins = origins.ToArray(),
            AllowBase44Subdomains = config.AllowBase44Subdomains,
            AllowLocalOrigins = config.AllowLocalOrigins,
        };
    }
}