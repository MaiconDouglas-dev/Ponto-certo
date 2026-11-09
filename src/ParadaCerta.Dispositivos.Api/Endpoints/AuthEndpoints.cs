using Microsoft.AspNetCore.Http.HttpResults;
using ParadaCerta.Dispositivos.Api.Configuracao;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Servicos;

namespace ParadaCerta.Dispositivos.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth")
            .WithTags("Autenticação")
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Login)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        grupo.MapPost("/dispositivo", LoginDispositivoAsync)
            .WithSummary("Login do totem")
            .WithDescription("Troca o identificador e o segredo gravados no firmware por um JWT de perfil DISPOSITIVO.");

        grupo.MapPost("/gestor", LoginGestorAsync)
            .WithSummary("Login do gestor")
            .WithDescription("Valida e-mail e senha de um usuário com perfil GESTOR e devolve um JWT.");

        return app;
    }

    private static async Task<Results<Ok<TokenResponse>, ValidationProblem, ProblemHttpResult>> LoginDispositivoAsync(
        LoginDispositivoRequest request, AuthService auth, CancellationToken ct)
    {
        var erros = LoginValidator.Validar(request);
        if (erros.Count > 0) return TypedResults.ValidationProblem(erros);

        var token = await auth.LoginDispositivoAsync(request, ct);
        return token is null
            ? Problemas.NaoAutorizado("Identificador ou segredo inválido, ou totem inativo.")
            : TypedResults.Ok(token);
    }

    private static async Task<Results<Ok<TokenResponse>, ValidationProblem, ProblemHttpResult>> LoginGestorAsync(
        LoginGestorRequest request, AuthService auth, CancellationToken ct)
    {
        var erros = LoginValidator.Validar(request);
        if (erros.Count > 0) return TypedResults.ValidationProblem(erros);

        var token = await auth.LoginGestorAsync(request, ct);
        return token is null
            ? Problemas.NaoAutorizado("E-mail ou senha inválidos.")
            : TypedResults.Ok(token);
    }
}
