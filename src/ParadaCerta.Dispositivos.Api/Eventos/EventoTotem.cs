using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ParadaCerta.Dispositivos.Api.Eventos;

public static class TiposEvento
{
    public const string SinalRecebido = "SINAL_RECEBIDO";
    public const string SinalRepetido = "SINAL_REPETIDO";
    public const string Heartbeat = "HEARTBEAT";
}

/// <summary>Documento da coleção eventos_totem (telemetria e histórico bruto dos totens).</summary>
public sealed class EventoTotem
{
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("tipo")] public string Tipo { get; set; } = string.Empty;
    [BsonElement("totemIdentificador")] public string TotemIdentificador { get; set; } = string.Empty;
    [BsonElement("ocorridoEm")] public DateTime OcorridoEm { get; set; }

    [BsonElement("sinalId"), BsonIgnoreIfNull] public long? SinalId { get; set; }
    [BsonElement("chaveIdempotencia"), BsonIgnoreIfNull] public string? ChaveIdempotencia { get; set; }
    [BsonElement("tipoSinal"), BsonIgnoreIfNull] public string? TipoSinal { get; set; }
    [BsonElement("rssiDbm"), BsonIgnoreIfNull] public int? RssiDbm { get; set; }
    [BsonElement("firmwareVersao"), BsonIgnoreIfNull] public string? FirmwareVersao { get; set; }
    [BsonElement("uptimeSegundos"), BsonIgnoreIfNull] public long? UptimeSegundos { get; set; }
}
