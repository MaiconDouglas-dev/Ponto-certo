namespace ParadaCerta.Dispositivos.Tests.Infra;

/// <summary>
/// Calculado fora do .NET (Python: hmac.new(chave, b"04A1B2C3D4", hashlib.sha256).hexdigest()),
/// para provar que o formato não depende da linguagem. Use o mesmo par na API Java.
/// </summary>
public static class ReferenciaHmac
{
    public const string Esperado = "f2eb07590200c3b0cc629c26b51b3f681d1203a0edd18d675800e0b9c065e2c9";

    /// <summary>
    /// Segredo do totem "segredo-de-teste" com a mesma chave
    /// (Python: hmac.new(chave, b"segredo-de-teste", hashlib.sha256).hexdigest()).
    /// É o que o HashService da API Java grava em TOTEM.TOKEN_HASH.
    /// </summary>
    public const string SegredoTotemEsperado = "8bcd585d724888d5060d1a509df1430f8f2ec89ca5a78bb32afa76f5b27669a1";
}
