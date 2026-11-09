using Microsoft.AspNetCore.Http.HttpResults;
using ParadaCerta.Dispositivos.Api.Configuracao;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Seguranca;
using ParadaCerta.Dispositivos.Api.Servicos;

namespace ParadaCerta.Dispositivos.Api.Endpoints;

/// <summary>RF16: o gestor cadastra, ativa, desativa e acompanha os totens.</summary>
public static class TotensEndpoints
{
    public static IEndpointRouteBuilder MapTotensEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/totens")
            .WithTags("Totens")
            .RequireAuthorization(Politicas.Gestor)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        grupo.MapGet("/", ListarAsync).WithSummary("Lista os totens (filtro opcional por status)");
        grupo.MapGet("/{id:long}", ObterAsync).WithSummary("Estado do totem, com último sinal e último heartbeat");
        grupo.MapPost("/", CriarAsync)
            .WithSummary("Cadastra um totem e devolve o segredo (só uma vez)")
            .ProducesProblem(StatusCodes.Status409Conflict);
        grupo.MapPatch("/{id:long}/status", AlterarStatusAsync).WithSummary("Ativa ou desativa o totem");
        grupo.MapPost("/{id:long}/segredo", RenovarSegredoAsync).WithSummary("Gera um novo segredo para o totem");
        grupo.MapGet("/{id:long}/sinais", ListarSinaisAsync).WithSummary("Últimos sinais recebidos do totem");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<TotemResponse>>, ValidationProblem>> ListarAsync(
        string? status, TotemService servico, CancellationToken ct)
    {
        var filtro = TotemService.ConverterStatus(status);
        if (status is not null && filtro is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Use ATIVO ou INATIVO."] });

        return TypedResults.Ok(await servico.ListarAsync(filtro, ct));
    }

    private static async Task<Results<Ok<TotemDetalheResponse>, NotFound>> ObterAsync(
        long id, TotemService servico, CancellationToken ct)
    {
        var totem = await servico.ObterAsync(id, ct);
        return totem is null ? TypedResults.NotFound() : TypedResults.Ok(totem);
    }

    private static async Task<Results<Created<TotemComSegredoResponse>, ValidationProblem, ProblemHttpResult>> CriarAsync(
        CriarTotemRequest request, TotemService servico, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(request, ct);
        return resultado.Tipo switch
        {
            ResultadoTotemTipo.Ok => TypedResults.Created($"/api/totens/{resultado.Valor!.Totem.Id}", resultado.Valor),
            ResultadoTotemTipo.Invalido => TypedResults.ValidationProblem(resultado.Erros!),
            ResultadoTotemTipo.Duplicado => Problemas.Conflito("Já existe um totem com esse identificador."),
            _ => throw new InvalidOperationException($"Resultado não tratado: {resultado.Tipo}")
        };
    }

    private static async Task<Results<Ok<TotemResponse>, NotFound, ValidationProblem>> AlterarStatusAsync(
        long id, AlterarStatusTotemRequest request, TotemService servico, CancellationToken ct)
    {
        var resultado = await servico.AlterarStatusAsync(id, request, ct);
        return resultado.Tipo switch
        {
            ResultadoTotemTipo.Ok => TypedResults.Ok(resultado.Valor!),
            ResultadoTotemTipo.NaoEncontrado => TypedResults.NotFound(),
            ResultadoTotemTipo.Invalido => TypedResults.ValidationProblem(resultado.Erros!),
            _ => throw new InvalidOperationException($"Resultado não tratado: {resultado.Tipo}")
        };
    }

    private static async Task<Results<Ok<TotemComSegredoResponse>, NotFound>> RenovarSegredoAsync(
        long id, TotemService servico, CancellationToken ct)
    {
        var resultado = await servico.RenovarSegredoAsync(id, ct);
        return resultado.Tipo == ResultadoTotemTipo.Ok ? TypedResults.Ok(resultado.Valor!) : TypedResults.NotFound();
    }

    private static async Task<Results<Ok<IReadOnlyList<SinalResponse>>, NotFound, ValidationProblem>> ListarSinaisAsync(
        long id, int? limite, TotemService totens, SinalService sinais, CancellationToken ct)
    {
        var quantidade = limite ?? 20;
        if (quantidade is < 1 or > 100)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["limite"] = ["Use um valor entre 1 e 100."] });

        if (!await totens.ExisteAsync(id, ct)) return TypedResults.NotFound();
        return TypedResults.Ok(await sinais.ListarPorTotemAsync(id, quantidade, ct));
    }
}
