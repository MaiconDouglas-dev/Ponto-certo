using System.Net.Sockets;
using Microsoft.AspNetCore.Diagnostics;
using MongoDB.Driver;
using Oracle.ManagedDataAccess.Client;
using RabbitMQ.Client.Exceptions;

namespace ParadaCerta.Dispositivos.Api.Configuracao;

/// <summary>
/// Reconhece exceções que significam "uma dependência (Oracle, RabbitMQ, MongoDB) está fora do ar",
/// e não um erro de programação. Percorre as exceções internas, porque os drivers costumam embrulhá-las.
/// </summary>
public static class FalhaDeDependencia
{
    /// <summary>Erros de conexão do Oracle: listener, serviço, rede, instância subindo e pool esgotado.</summary>
    private static readonly HashSet<int> ErrosDeConexaoOracle =
    [
        1033, 1034, 1089, 3113, 3114, 3135,
        12154, 12170, 12514, 12516, 12519, 12520, 12521, 12528, 12537, 12541, 12543, 12545, 12560,
        50000, 50201
    ];

    public static bool Detectar(Exception? excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            switch (atual)
            {
                case TimeoutException:
                case SocketException:
                case BrokerUnreachableException:
                case MongoConnectionException:
                case OracleException ora when ErrosDeConexaoOracle.Contains(ora.Number):
                    return true;
                case AggregateException agregada when agregada.InnerExceptions.Any(Detectar):
                    return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Dependência fora do ar vira 503 com Retry-After (o cliente sabe que pode tentar de novo),
/// em vez do 500 genérico. Os demais erros seguem para o tratamento padrão (500).
/// </summary>
public sealed class TratadorFalhaDeDependencia(
    IProblemDetailsService problemas,
    ILogger<TratadorFalhaDeDependencia> logger) : IExceptionHandler
{
    public const int SegundosParaTentarDeNovo = 15;

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excecao, CancellationToken ct)
    {
        if (!FalhaDeDependencia.Detectar(excecao)) return false;

        logger.LogWarning(excecao, "Dependência indisponível ao atender {Metodo} {Rota}.",
            http.Request.Method, http.Request.Path);

        http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        http.Response.Headers.RetryAfter = SegundosParaTentarDeNovo.ToString();

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = excecao,
            ProblemDetails =
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Serviço indisponível",
                Detail = "Um serviço de que a API depende está fora do ar. Tente novamente em instantes."
            }
        });
    }
}
