namespace ParadaCerta.Dispositivos.Api.Eventos;

public interface IRegistroEventos
{
    Task RegistrarAsync(EventoTotem evento, CancellationToken ct);
    Task<DateTime?> UltimoHeartbeatAsync(string totemIdentificador, CancellationToken ct);
}
