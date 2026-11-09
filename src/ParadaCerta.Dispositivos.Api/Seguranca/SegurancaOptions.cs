using System.ComponentModel.DataAnnotations;

namespace ParadaCerta.Dispositivos.Api.Seguranca;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    [Required] public string Emissor { get; set; } = "parada-certa-dispositivos";
    [Required] public string Audiencia { get; set; } = "parada-certa";

    /// <summary>Chave HMAC do JWT. Mínimo de 32 caracteres. Nunca versionar o valor real.</summary>
    [Required, MinLength(32)] public string Chave { get; set; } = string.Empty;

    [Range(1, 720)] public int ExpiracaoDispositivoHoras { get; set; } = 24;
    [Range(5, 1440)] public int ExpiracaoGestorMinutos { get; set; } = 120;
}

public sealed class CartaoOptions
{
    public const string Secao = "Cartao";

    /// <summary>
    /// Segredo do HMAC-SHA256 do UID do cartão. Precisa ser IGUAL ao usado pela API Java,
    /// senão o hash gravado aqui nunca bate com o do cartão cadastrado pelo cuidador.
    /// </summary>
    [Required, MinLength(32)] public string ChaveHmac { get; set; } = string.Empty;
}
