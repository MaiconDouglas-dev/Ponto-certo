using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ParadaCerta.Dispositivos.Api.Seguranca;

public interface IHashCartao
{
    /// <summary>HMAC-SHA256 do UID normalizado, em hex minúsculo (64 caracteres).</summary>
    string Calcular(string uidCartao);
}

/// <summary>
/// Regra combinada com a API Java:
/// 1. normaliza o UID: remove espaços, ':' e '-', e passa para MAIÚSCULAS ("04:a1:b2" vira "04A1B2");
/// 2. calcula HMAC-SHA256 dos bytes UTF-8 do texto normalizado, com a chave Cartao:ChaveHmac;
/// 3. converte o resultado para hex minúsculo.
/// </summary>
public sealed class HashCartao(IOptions<CartaoOptions> opcoes) : IHashCartao
{
    private readonly byte[] _chave = Encoding.UTF8.GetBytes(opcoes.Value.ChaveHmac);

    public string Calcular(string uidCartao)
    {
        var normalizado = NormalizarUid(uidCartao);
        var hash = HMACSHA256.HashData(_chave, Encoding.UTF8.GetBytes(normalizado));
        return Convert.ToHexStringLower(hash);
    }

    public static string NormalizarUid(string uid) =>
        uid.Replace(" ", string.Empty)
           .Replace(":", string.Empty)
           .Replace("-", string.Empty)
           .Trim()
           .ToUpperInvariant();
}

public interface IHashSegredo
{
    /// <summary>HMAC-SHA256 do segredo do totem, em hex minúsculo (64 caracteres).</summary>
    string Calcular(string segredo);

    /// <summary>Confere o segredo enviado pelo totem com o hash gravado.</summary>
    bool Confere(string segredo, string hashGravado);
}

/// <summary>
/// Hash da coluna TOTEM.TOKEN_HASH, igual ao HashService da API Java:
/// HMAC-SHA256 dos bytes UTF-8 do segredo sem espaços nas pontas, com a mesma chave do cartão
/// (Cartao:ChaveHmac = HASH_SECRET do Java), em hex minúsculo. Assim um totem cadastrado
/// por qualquer uma das duas APIs consegue entrar pela outra.
/// </summary>
public sealed class HashSegredo(IOptions<CartaoOptions> opcoes) : IHashSegredo
{
    private readonly byte[] _chave = Encoding.UTF8.GetBytes(opcoes.Value.ChaveHmac);

    public string Calcular(string segredo) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_chave, Encoding.UTF8.GetBytes(segredo.Trim())));

    /// <summary>Compara em tempo constante, para não vazar informação pelo tempo de resposta.</summary>
    public bool Confere(string segredo, string hashGravado)
    {
        var calculado = Encoding.ASCII.GetBytes(Calcular(segredo));
        var gravado = Encoding.ASCII.GetBytes(hashGravado.Trim());
        return CryptographicOperations.FixedTimeEquals(calculado, gravado);
    }

    /// <summary>Gera um segredo aleatório de 32 bytes, em Base64 URL-safe.</summary>
    public static string GerarSegredo() =>
        System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
}
