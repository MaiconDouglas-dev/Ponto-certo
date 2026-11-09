using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Dominio;

namespace ParadaCerta.Dispositivos.Api.Seguranca;

public sealed class TokenService(IOptions<JwtOptions> opcoes, TimeProvider relogio)
{
    private readonly JwtOptions _jwt = opcoes.Value;

    public TokenResponse GerarParaDispositivo(Totem totem) =>
        Gerar(
            sujeito: $"totem:{totem.Id}",
            perfil: Perfis.Dispositivo,
            validade: TimeSpan.FromHours(_jwt.ExpiracaoDispositivoHoras),
            new Claim(ClaimsParadaCerta.TotemId, totem.Identificador));

    public TokenResponse GerarParaGestor(Usuario usuario) =>
        Gerar(
            sujeito: $"usuario:{usuario.Id}",
            perfil: Perfis.Gestor,
            validade: TimeSpan.FromMinutes(_jwt.ExpiracaoGestorMinutos),
            new Claim(JwtRegisteredClaimNames.Name, usuario.Nome));

    private TokenResponse Gerar(string sujeito, string perfil, TimeSpan validade, params Claim[] extras)
    {
        var agora = relogio.GetUtcNow();
        var expiraEm = agora.Add(validade);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, sujeito),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimsParadaCerta.Perfil, perfil)
        };
        claims.AddRange(extras);

        var descritor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Emissor,
            Audience = _jwt.Audiencia,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(CriarChave(_jwt.Chave), SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descritor);
        return new TokenResponse(token, "Bearer", expiraEm, perfil);
    }

    public static SymmetricSecurityKey CriarChave(string chave) => new(Encoding.UTF8.GetBytes(chave));
}
