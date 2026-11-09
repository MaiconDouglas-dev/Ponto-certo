using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ParadaCerta.Dispositivos.Api.Seguranca;

namespace ParadaCerta.Dispositivos.Api.Configuracao;

public static class PoliticasLimite
{
    public const string Sinais = "sinais";
    public const string Heartbeat = "heartbeat";
    public const string Login = "login";
}

public sealed class LimiteRequisicoesOptions
{
    public const string Secao = "LimiteRequisicoes";

    public JanelaLimite Sinais { get; set; } = new() { Limite = 10, JanelaSegundos = 60 };
    public JanelaLimite Heartbeat { get; set; } = new() { Limite = 6, JanelaSegundos = 60 };
    public JanelaLimite Login { get; set; } = new() { Limite = 5, JanelaSegundos = 60 };
}

public sealed class JanelaLimite
{
    public int Limite { get; set; }
    public int JanelaSegundos { get; set; }
}

public static class LimiteRequisicoesExtensions
{
    /// <summary>
    /// Rate limiting nativo do ASP.NET Core (janela fixa). Sinais e heartbeats são contados por totem
    /// (claim totem_id); login é contado por IP. Ao passar do limite responde 429 com Retry-After.
    /// </summary>
    public static IServiceCollection AddLimiteDeRequisicoes(this IServiceCollection services)
    {
        services.AddOptions<LimiteRequisicoesOptions>().BindConfiguration(LimiteRequisicoesOptions.Secao);

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = EscreverRespostaAsync;

            o.AddPolicy(PoliticasLimite.Sinais, http => PorTotem(http, l => l.Sinais));
            o.AddPolicy(PoliticasLimite.Heartbeat, http => PorTotem(http, l => l.Heartbeat));
            o.AddPolicy(PoliticasLimite.Login, http =>
                JanelaFixa("login:" + (http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido"), Ler(http, l => l.Login)));
        });

        return services;
    }

    private static RateLimitPartition<string> PorTotem(HttpContext http, Func<LimiteRequisicoesOptions, JanelaLimite> janela)
    {
        var totem = http.User.FindFirstValue(ClaimsParadaCerta.TotemId)
                    ?? http.Connection.RemoteIpAddress?.ToString()
                    ?? "desconhecido";
        return JanelaFixa($"{http.GetEndpoint()?.DisplayName}:{totem}", Ler(http, janela));
    }

    private static JanelaLimite Ler(HttpContext http, Func<LimiteRequisicoesOptions, JanelaLimite> janela) =>
        janela(http.RequestServices.GetRequiredService<IOptions<LimiteRequisicoesOptions>>().Value);

    private static RateLimitPartition<string> JanelaFixa(string chave, JanelaLimite janela) =>
        RateLimitPartition.GetFixedWindowLimiter(chave, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = janela.Limite,
            Window = TimeSpan.FromSeconds(janela.JanelaSegundos),
            QueueLimit = 0,
            AutoReplenishment = true
        });

    private static async ValueTask EscreverRespostaAsync(OnRejectedContext contexto, CancellationToken ct)
    {
        var http = contexto.HttpContext;
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var esperar))
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(esperar.TotalSeconds)).ToString();

        var problemas = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemas.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Muitas requisições",
                Detail = "Limite de requisições atingido. Aguarde o tempo indicado em Retry-After."
            }
        });
    }
}
