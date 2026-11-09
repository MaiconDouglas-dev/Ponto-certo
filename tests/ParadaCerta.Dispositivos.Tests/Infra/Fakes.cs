using System.Collections.Concurrent;
using ParadaCerta.Dispositivos.Api.Eventos;
using ParadaCerta.Dispositivos.Api.Mensageria;

namespace ParadaCerta.Dispositivos.Tests.Infra;

/// <summary>Substitui o RabbitMQ: guarda as mensagens em memória e pode simular falha.</summary>
public sealed class PublicadorFalso : IPublicadorSinais
{
    public ConcurrentQueue<MensagemSinalTotem> Publicadas { get; } = new();
    public bool Falhar { get; set; }

    public Task PublicarAsync(MensagemSinalTotem mensagem, CancellationToken ct)
    {
        if (Falhar) throw new InvalidOperationException("Broker fora do ar (simulado).");
        Publicadas.Enqueue(mensagem);
        return Task.CompletedTask;
    }
}

/// <summary>Substitui o MongoDB.</summary>
public sealed class RegistroEventosFalso : IRegistroEventos
{
    public ConcurrentQueue<EventoTotem> Eventos { get; } = new();

    public Task RegistrarAsync(EventoTotem evento, CancellationToken ct)
    {
        Eventos.Enqueue(evento);
        return Task.CompletedTask;
    }

    public Task<DateTime?> UltimoHeartbeatAsync(string totemIdentificador, CancellationToken ct) =>
        Task.FromResult(Eventos
            .Where(e => e.TotemIdentificador == totemIdentificador && e.Tipo == TiposEvento.Heartbeat)
            .Select(e => (DateTime?)e.OcorridoEm)
            .Max());
}
