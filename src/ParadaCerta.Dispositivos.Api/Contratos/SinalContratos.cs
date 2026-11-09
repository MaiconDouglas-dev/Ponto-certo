namespace ParadaCerta.Dispositivos.Api.Contratos;

/// <summary>
/// Corpo enviado pelo totem em POST /api/sinais.
/// Exemplo: {"totemId":"TOTEM-0001","tipo":"CARTAO","uidCartao":"04A1B2C3D4","dataHora":"2026-11-18T14:32:05-03:00"}
/// </summary>
public sealed record SinalRequest(string TotemId, string Tipo, string? UidCartao, DateTimeOffset? DataHora);

public sealed record SinalResponse(
    long Id,
    string ChaveIdempotencia,
    string TotemId,
    string Tipo,
    DateTime RecebidoEm,
    long? SolicitacaoId);

/// <summary>Corpo opcional do heartbeat (sinal de vida a cada minuto).</summary>
public sealed record HeartbeatRequest(int? RssiDbm, string? FirmwareVersao, long? UptimeSegundos);
