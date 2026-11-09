using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using ParadaCerta.Dispositivos.Api.Dominio;
using ParadaCerta.Dispositivos.Api.Seguranca;
using ParadaCerta.Dispositivos.Tests.Infra;

namespace ParadaCerta.Dispositivos.Tests.Unidade;

public class SegurancaTests
{
    private static HashCartao CriarHash(string chave) =>
        new(Options.Create(new CartaoOptions { ChaveHmac = chave }));

    [Fact]
    public void Hash_do_cartao_tem_64_caracteres_hex_minusculos()
    {
        var hash = CriarHash(DadosDeTeste.ChaveHmac).Calcular("04A1B2C3D4");
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void Hash_do_cartao_ignora_separadores_e_maiusculas()
    {
        var hash = CriarHash(DadosDeTeste.ChaveHmac);
        Assert.Equal(hash.Calcular("04A1B2C3D4"), hash.Calcular("04:a1:b2:c3:d4"));
    }

    [Fact]
    public void Hash_do_cartao_muda_com_a_chave_hmac()
    {
        var a = CriarHash(DadosDeTeste.ChaveHmac).Calcular("04A1B2C3D4");
        var b = CriarHash("outra-chave-hmac-com-mais-de-32-caracteres!").Calcular("04A1B2C3D4");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Hash_do_cartao_bate_com_valor_de_referencia_para_a_api_java()
    {
        // Valor de referência: HMAC-SHA256(chave="chave-hmac-de-teste-com-mais-de-32-caracteres", msg="04A1B2C3D4").
        // A API Java deve produzir exatamente o mesmo texto com os mesmos dados.
        var hash = CriarHash(DadosDeTeste.ChaveHmac).Calcular("04A1B2C3D4");
        Assert.Equal(ReferenciaHmac.Esperado, hash);
    }

    [Fact]
    public void Segredo_do_totem_confere_so_com_o_valor_certo()
    {
        var hash = HashSegredo.Calcular("meu-segredo");
        Assert.True(HashSegredo.Confere("meu-segredo", hash));
        Assert.False(HashSegredo.Confere("outro-segredo", hash));
    }

    [Fact]
    public void Segredos_gerados_sao_diferentes()
    {
        Assert.NotEqual(HashSegredo.GerarSegredo(), HashSegredo.GerarSegredo());
    }

    [Fact]
    public void Token_do_dispositivo_tem_perfil_totem_e_validade_configurada()
    {
        var relogio = new FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
        var servico = new TokenService(Options.Create(new JwtOptions
        {
            Chave = DadosDeTeste.ChaveJwt,
            ExpiracaoDispositivoHoras = 24
        }), relogio);

        var resposta = servico.GerarParaDispositivo(new Totem { Id = 7, Identificador = "TOTEM-0007" });
        var token = new JsonWebToken(resposta.AccessToken);

        Assert.Equal(Perfis.Dispositivo, resposta.Perfil);
        Assert.Equal(Perfis.Dispositivo, token.GetClaim(ClaimsParadaCerta.Perfil).Value);
        Assert.Equal("TOTEM-0007", token.GetClaim(ClaimsParadaCerta.TotemId).Value);
        Assert.Equal(relogio.GetUtcNow().AddHours(24), resposta.ExpiraEm);
    }
}
