using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ParadaCerta.Dispositivos.Api.Saude;

/// <summary>Escreve o resultado dos health checks em JSON legível.</summary>
public static class RespostaSaude
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Task EscreverAsync(HttpContext http, HealthReport relatorio)
    {
        http.Response.ContentType = "application/json; charset=utf-8";

        var corpo = new
        {
            status = relatorio.Status.ToString(),
            duracaoMs = Math.Round(relatorio.TotalDuration.TotalMilliseconds, 1),
            verificacoes = relatorio.Entries.Select(e => new
            {
                nome = e.Key,
                status = e.Value.Status.ToString(),
                descricao = e.Value.Description,
                erro = e.Value.Exception?.Message,
                duracaoMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1)
            })
        };

        return http.Response.WriteAsync(JsonSerializer.Serialize(corpo, Json));
    }
}
