using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace ParadaCerta.Dispositivos.Api.Eventos;

public sealed class MongoRegistroEventos(IMongoDatabase banco, IOptions<MongoOptions> opcoes) : IRegistroEventos
{
    private readonly IMongoCollection<EventoTotem> _colecao =
        banco.GetCollection<EventoTotem>(opcoes.Value.ColecaoEventos);

    public Task RegistrarAsync(EventoTotem evento, CancellationToken ct) =>
        _colecao.InsertOneAsync(evento, cancellationToken: ct);

    public async Task<DateTime?> UltimoHeartbeatAsync(string totemIdentificador, CancellationToken ct)
    {
        var ultimo = await _colecao
            .Find(e => e.TotemIdentificador == totemIdentificador && e.Tipo == TiposEvento.Heartbeat)
            .SortByDescending(e => e.OcorridoEm)
            .Limit(1)
            .FirstOrDefaultAsync(ct);

        return ultimo?.OcorridoEm;
    }
}
