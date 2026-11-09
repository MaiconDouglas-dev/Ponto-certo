using ParadaCerta.Dispositivos.Api.Contratos;

namespace ParadaCerta.Dispositivos.Api.Servicos;

/// <summary>Campos vazios no login são erro de entrada (400), não credencial errada (401).</summary>
public static class LoginValidator
{
    public static Dictionary<string, string[]> Validar(LoginDispositivoRequest request) =>
        Obrigatorios(("identificador", request.Identificador), ("segredo", request.Segredo));

    public static Dictionary<string, string[]> Validar(LoginGestorRequest request) =>
        Obrigatorios(("email", request.Email), ("senha", request.Senha));

    private static Dictionary<string, string[]> Obrigatorios(params (string Campo, string? Valor)[] campos) =>
        campos.Where(c => string.IsNullOrWhiteSpace(c.Valor))
              .ToDictionary(c => c.Campo, c => new[] { $"{c.Campo} é obrigatório." });
}
