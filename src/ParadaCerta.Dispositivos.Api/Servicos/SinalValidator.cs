using System.Text.RegularExpressions;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Dominio;
using ParadaCerta.Dispositivos.Api.Seguranca;

namespace ParadaCerta.Dispositivos.Api.Servicos;

/// <summary>Validações de entrada do POST /api/sinais. Retorna os erros por campo (vazio = válido).</summary>
public static partial class SinalValidator
{
    public const int TamanhoMaximoChave = 80;

    public static Dictionary<string, string[]> Validar(SinalRequest request, string? chaveIdempotencia)
    {
        var erros = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(chaveIdempotencia))
            erros["Idempotency-Key"] = ["O cabeçalho Idempotency-Key é obrigatório."];
        else if (chaveIdempotencia.Length > TamanhoMaximoChave || !ChaveValida().IsMatch(chaveIdempotencia))
            erros["Idempotency-Key"] = [$"Use até {TamanhoMaximoChave} caracteres: letras, números, '-', '_', '.' ou ':'."];

        if (string.IsNullOrWhiteSpace(request.TotemId))
            erros[nameof(request.TotemId)] = ["totemId é obrigatório."];

        var tipo = ConverterTipo(request.Tipo);
        if (tipo is null)
            erros[nameof(request.Tipo)] = ["tipo deve ser CARTAO ou BOTAO."];

        if (tipo == TipoSinal.Cartao)
        {
            if (string.IsNullOrWhiteSpace(request.UidCartao))
                erros[nameof(request.UidCartao)] = ["uidCartao é obrigatório quando o tipo é CARTAO."];
            else if (!UidValido().IsMatch(HashCartao.NormalizarUid(request.UidCartao)))
                erros[nameof(request.UidCartao)] = ["uidCartao deve ter de 4 a 10 bytes em hexadecimal (ex.: 04A1B2C3D4)."];
        }
        else if (tipo == TipoSinal.Botao && !string.IsNullOrWhiteSpace(request.UidCartao))
        {
            erros[nameof(request.UidCartao)] = ["uidCartao não deve ser enviado quando o tipo é BOTAO."];
        }

        if (request.DataHora is null)
            erros[nameof(request.DataHora)] = ["dataHora é obrigatória (ISO 8601 com fuso, ex.: 2026-11-18T14:32:05-03:00)."];

        return erros;
    }

    public static TipoSinal? ConverterTipo(string? tipo) => tipo?.Trim().ToUpperInvariant() switch
    {
        "CARTAO" => TipoSinal.Cartao,
        "BOTAO" => TipoSinal.Botao,
        _ => null
    };

    [GeneratedRegex("^[A-Za-z0-9_.:-]+$")]
    private static partial Regex ChaveValida();

    [GeneratedRegex("^([0-9A-F]{2}){4,10}$")]
    private static partial Regex UidValido();
}
