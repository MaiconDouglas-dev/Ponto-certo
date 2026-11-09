namespace ParadaCerta.Dispositivos.Api.Dominio;

/// <summary>Tabela USUARIO. A API .NET só lê, para autenticar o GESTOR.</summary>
public class Usuario
{
    public long Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash BCrypt, o mesmo formato gerado pela API Java (Spring Security).</summary>
    public string SenhaHash { get; set; } = string.Empty;

    public string Perfil { get; set; } = string.Empty;
}
