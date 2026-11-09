using ParadaCerta.Dispositivos.Api.Dados;
using ParadaCerta.Dispositivos.Api.Dominio;
using ParadaCerta.Dispositivos.Api.Seguranca;

namespace ParadaCerta.Dispositivos.Tests.Infra;

public static class DadosDeTeste
{
    public const string ChaveJwt = "chave-jwt-de-teste-com-mais-de-32-caracteres";
    public const string ChaveHmac = "chave-hmac-de-teste-com-mais-de-32-caracteres";

    public const string TotemAtivo = "TOTEM-0001";
    public const string TotemLimite = "TOTEM-0002";
    public const string TotemInativo = "TOTEM-0003";
    public const string SegredoTotem = "segredo-de-teste";

    public const string EmailGestor = "gestor@paradacerta.test";
    public const string SenhaGestor = "Senha@123";

    public static void Popular(ParadaCertaDbContext db)
    {
        var parada = new Parada { CodigoSptrans = 340015329, Nome = "Av. Paulista, 1000" };
        db.Paradas.Add(parada);
        db.SaveChanges();

        db.Totens.AddRange(
            NovoTotem(TotemAtivo, parada.Id, StatusTotem.Ativo),
            NovoTotem(TotemLimite, parada.Id, StatusTotem.Ativo),
            NovoTotem(TotemInativo, parada.Id, StatusTotem.Inativo));

        db.Usuarios.AddRange(
            new Usuario { Nome = "Gestor Teste", Email = EmailGestor, Perfil = Perfis.Gestor, SenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaGestor, 4) },
            new Usuario { Nome = "Cuidadora Teste", Email = "cuidadora@paradacerta.test", Perfil = "CUIDADOR", SenhaHash = BCrypt.Net.BCrypt.HashPassword(SenhaGestor, 4) });

        db.SaveChanges();
    }

    private static Totem NovoTotem(string identificador, long paradaId, StatusTotem status) => new()
    {
        Identificador = identificador,
        ParadaId = paradaId,
        TokenHash = HashSegredo.Calcular(SegredoTotem),
        Status = status,
        CriadoEm = DateTime.UtcNow
    };
}
