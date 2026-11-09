using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using ParadaCerta.Dispositivos.Api.Configuracao;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Eventos;
using ParadaCerta.Dispositivos.Api.Seguranca;

namespace ParadaCerta.Dispositivos.Api.Endpoints;

public static class HeartbeatEndpoints
{
    public static IEndpointRouteBuilder MapHeartbeatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/heartbeats", RegistrarAsync)
            .WithTags("Sinais")
            .RequireAuthorization(Politicas.Dispositivo)
            .RequireRateLimiting(PoliticasLimite.Heartbeat)
            .WithSummary("Sinal de vida do totem")
            .WithDescription("Gravado só no MongoDB (coleção eventos_totem). O gestor usa para saber se o totem está online.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RegistrarAsync(
        HeartbeatRequest? request,
        ClaimsPrincipal usuario,
        IRegistroEventos eventos,
        TimeProvider relogio,
        CancellationToken ct)
    {
        try
        {
            await eventos.RegistrarAsync(new EventoTotem
            {
                Tipo = TiposEvento.Heartbeat,
                TotemIdentificador = usuario.FindFirstValue(ClaimsParadaCerta.TotemId)!,
                OcorridoEm = relogio.GetUtcNow().UtcDateTime,
                RssiDbm = request?.RssiDbm,
                FirmwareVersao = request?.FirmwareVersao,
                UptimeSegundos = request?.UptimeSegundos
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Problemas.Indisponivel("Não foi possível registrar o heartbeat.");
        }

        return TypedResults.NoContent();
    }
}
