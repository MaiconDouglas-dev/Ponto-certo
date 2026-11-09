using Microsoft.EntityFrameworkCore;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Dados;
using ParadaCerta.Dispositivos.Api.Dominio;
using ParadaCerta.Dispositivos.Api.Seguranca;

namespace ParadaCerta.Dispositivos.Api.Servicos;

public sealed class AuthService(ParadaCertaDbContext db, TokenService tokens)
{
    /// <summary>Troca identificador + segredo do totem por um JWT de perfil DISPOSITIVO.</summary>
    public async Task<TokenResponse?> LoginDispositivoAsync(LoginDispositivoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Identificador) || string.IsNullOrWhiteSpace(request.Segredo))
            return null;

        var identificador = request.Identificador.Trim().ToUpperInvariant();
        var totem = await db.Totens.AsNoTracking().FirstOrDefaultAsync(t => t.Identificador == identificador, ct);

        if (totem is null || totem.Status != StatusTotem.Ativo) return null;
        if (!HashSegredo.Confere(request.Segredo, totem.TokenHash)) return null;

        return tokens.GerarParaDispositivo(totem);
    }

    /// <summary>Valida o gestor na tabela USUARIO (senha em BCrypt, compatível com o Spring Security).</summary>
    public async Task<TokenResponse?> LoginGestorAsync(LoginGestorRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Senha))
            return null;

        var email = request.Email.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        if (usuario is null || usuario.Perfil != Perfis.Gestor) return null;
        if (!BCrypt.Net.BCrypt.Verify(request.Senha, usuario.SenhaHash)) return null;

        return tokens.GerarParaGestor(usuario);
    }
}
