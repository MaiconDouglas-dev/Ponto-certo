using Microsoft.AspNetCore.Http.HttpResults;

namespace ParadaCerta.Dispositivos.Api.Configuracao;

/// <summary>Atalhos para respostas de erro no formato Problem Details (RFC 9457).</summary>
public static class Problemas
{
    public static ProblemHttpResult NaoAutorizado(string detalhe) =>
        TypedResults.Problem(detalhe, statusCode: StatusCodes.Status401Unauthorized, title: "Não autorizado");

    public static ProblemHttpResult Proibido(string detalhe) =>
        TypedResults.Problem(detalhe, statusCode: StatusCodes.Status403Forbidden, title: "Acesso negado");

    public static ProblemHttpResult Conflito(string detalhe) =>
        TypedResults.Problem(detalhe, statusCode: StatusCodes.Status409Conflict, title: "Conflito");

    public static ProblemHttpResult Indisponivel(string detalhe) =>
        TypedResults.Problem(detalhe, statusCode: StatusCodes.Status503ServiceUnavailable, title: "Serviço indisponível");
}
