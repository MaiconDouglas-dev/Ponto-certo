namespace ParadaCerta.Dispositivos.Api.Dominio;

/// <summary>Tabela TOTEM: o ESP32 instalado na parada.</summary>
public class Totem
{
    public long Id { get; set; }
    public long ParadaId { get; set; }
    public string Identificador { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 (hex minúsculo) do segredo do dispositivo, igual ao da API Java. O segredo puro nunca é gravado.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public StatusTotem Status { get; set; } = StatusTotem.Ativo;
    public DateTime? UltimoSinalEm { get; set; }
    public DateTime CriadoEm { get; set; }

    public Parada? Parada { get; set; }
}
