namespace ParadaCerta.Dispositivos.Api.Contratos;

public sealed record CriarTotemRequest(string Identificador, long ParadaId);

public sealed record AlterarStatusTotemRequest(string Status);

public sealed record TotemResponse(
    long Id,
    string Identificador,
    long ParadaId,
    string? ParadaNome,
    string Status,
    DateTime? UltimoSinalEm,
    DateTime CriadoEm);

public sealed record TotemDetalheResponse(
    long Id,
    string Identificador,
    long ParadaId,
    string? ParadaNome,
    string Status,
    DateTime? UltimoSinalEm,
    DateTime? UltimoHeartbeatEm,
    bool Online,
    DateTime CriadoEm);

/// <summary>
/// Devolvido só na criação do totem ou na troca de segredo.
/// O segredo não fica salvo em lugar nenhum: ele deve ser gravado no firmware.
/// </summary>
public sealed record TotemComSegredoResponse(TotemResponse Totem, string Segredo);
