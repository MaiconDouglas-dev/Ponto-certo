namespace ParadaCerta.Dispositivos.Api.Mensageria;

/// <summary>
/// Contrato da mensagem publicada na fila "sinal-totem" e consumida pela API Java.
/// Qualquer mudança aqui precisa ser combinada com quem faz o consumidor.
/// </summary>
public sealed record MensagemSinalTotem(
    long SinalId,
    string ChaveIdempotencia,
    long TotemId,
    string TotemIdentificador,
    long ParadaId,
    string Tipo,
    string? UidHash,
    DateTimeOffset? DataHoraTotem,
    DateTime RecebidoEm);
