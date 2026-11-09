using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParadaCerta.Dispositivos.Api.Mensageria;

namespace ParadaCerta.Dispositivos.Api.Saude;

public sealed class RabbitMqHealthCheck(RabbitMqConexao conexao) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var aberta = await conexao.ObterAsync(ct);
            return aberta.IsOpen
                ? HealthCheckResult.Healthy("Conectado ao RabbitMQ.")
                : HealthCheckResult.Unhealthy("Conexão com o RabbitMQ fechada.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Não foi possível conectar ao RabbitMQ.", ex);
        }
    }
}
