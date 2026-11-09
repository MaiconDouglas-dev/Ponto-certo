using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ParadaCerta.Dispositivos.Api.Configuracao;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Seguranca;
using ParadaCerta.Dispositivos.Api.Servicos;

namespace ParadaCerta.Dispositivos.Api.Endpoints;

public static class SinaisEndpoints
{
    public const string CabecalhoRepeticao = "Idempotency-Replayed";

    public static IEndpointRouteBuilder MapSinaisEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/sinais")
            .WithTags("Sinais")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        grupo.MapPost("/", ReceberAsync)
            .RequireAuthorization(Politicas.Dispositivo)
            .RequireRateLimiting(PoliticasLimite.Sinais)
            .WithSummary("Recebe um sinal do totem (cartão ou botão)")
            .WithDescription(
                "Exige o cabeçalho Idempotency-Key. Chave nova: 201 e publica na fila sinal-totem. " +
                "Mesma chave com o mesmo conteúdo: 200 com o mesmo sinal, sem novo registro. " +
                "Mesma chave com outro conteúdo: 409. Totem diferente do token, inativo ou não cadastrado: 403. " +
                "Mais de 10 sinais por minuto do mesmo totem: 429. Fila fora do ar: 503 (reenvie com a mesma chave).")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        grupo.MapGet("/{id:long}", ObterAsync)
            .RequireAuthorization(Politicas.Gestor)
            .WithName("ObterSinal")
            .WithSummary("Consulta um sinal pelo id");

        return app;
    }

    private static async Task<Results<Created<SinalResponse>, Ok<SinalResponse>, ValidationProblem, ProblemHttpResult>> ReceberAsync(
        SinalRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? chaveIdempotencia,
        ClaimsPrincipal usuario,
        HttpResponse resposta,
        SinalService servico,
        CancellationToken ct)
    {
        var erros = SinalValidator.Validar(request, chaveIdempotencia);
        if (erros.Count > 0) return TypedResults.ValidationProblem(erros);

        if (usuario.FindFirstValue(ClaimsParadaCerta.TotemId) != request.TotemId)
            return Problemas.Proibido("O totemId do corpo é diferente do totem autenticado.");

        var resultado = await servico.ReceberAsync(request, chaveIdempotencia!, ct);

        switch (resultado.Tipo)
        {
            case ResultadoSinalTipo.Criado:
                return TypedResults.Created($"/api/sinais/{resultado.Sinal!.Id}", resultado.Sinal);
            case ResultadoSinalTipo.Repetido:
                resposta.Headers[CabecalhoRepeticao] = "true";
                return TypedResults.Ok(resultado.Sinal!);
            case ResultadoSinalTipo.ChaveEmConflito:
                return Problemas.Conflito("Esta Idempotency-Key já foi usada com outro conteúdo. Gere uma chave nova.");
            case ResultadoSinalTipo.TotemNaoEncontrado:
                return Problemas.Proibido("Totem não cadastrado.");
            case ResultadoSinalTipo.TotemInativo:
                return Problemas.Proibido("Totem inativo. Fale com o gestor.");
            case ResultadoSinalTipo.MensageriaIndisponivel:
                return Problemas.Indisponivel("Fila de mensagens indisponível. Reenvie com a mesma Idempotency-Key.");
            default:
                throw new InvalidOperationException($"Resultado não tratado: {resultado.Tipo}");
        }
    }

    private static async Task<Results<Ok<SinalResponse>, NotFound>> ObterAsync(long id, SinalService servico, CancellationToken ct)
    {
        var sinal = await servico.ObterAsync(id, ct);
        return sinal is null ? TypedResults.NotFound() : TypedResults.Ok(sinal);
    }
}
