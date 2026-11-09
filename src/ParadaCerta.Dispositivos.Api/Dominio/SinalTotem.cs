namespace ParadaCerta.Dispositivos.Api.Dominio;

/// <summary>
/// Tabela SINAL_TOTEM: cada sinal ACEITO (cartão ou botão).
/// A coluna CHAVE_IDEMPOTENCIA é UNIQUE e garante que o mesmo envio não gere dois registros.
/// </summary>
public class SinalTotem
{
    public long Id { get; set; }
    public long TotemId { get; set; }
    public string ChaveIdempotencia { get; set; } = string.Empty;
    public TipoSinal Tipo { get; set; }

    /// <summary>HMAC-SHA256 (hex minúsculo) do UID do cartão. Nulo quando o sinal é de botão.</summary>
    public string? UidHash { get; set; }

    /// <summary>Preenchido pela API Java quando ela cria a solicitação de embarque.</summary>
    public long? SolicitacaoId { get; set; }

    public DateTime RecebidoEm { get; set; }

    public Totem? Totem { get; set; }
}
