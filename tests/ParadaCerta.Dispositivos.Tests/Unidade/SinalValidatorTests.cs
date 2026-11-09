using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Servicos;

namespace ParadaCerta.Dispositivos.Tests.Unidade;

public class SinalValidatorTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 18, 14, 32, 5, TimeSpan.FromHours(-3));

    [Fact]
    public void Sinal_de_cartao_valido_nao_tem_erros()
    {
        var erros = SinalValidator.Validar(new SinalRequest("TOTEM-0001", "CARTAO", "04A1B2C3D4", Agora), "TOTEM-0001-000123");
        Assert.Empty(erros);
    }

    [Fact]
    public void Sinal_de_botao_sem_uid_e_valido()
    {
        var erros = SinalValidator.Validar(new SinalRequest("TOTEM-0001", "botao", null, Agora), "TOTEM-0001-000124");
        Assert.Empty(erros);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("chave com espaço")]
    public void Chave_de_idempotencia_ausente_ou_invalida_gera_erro(string? chave)
    {
        var erros = SinalValidator.Validar(new SinalRequest("TOTEM-0001", "BOTAO", null, Agora), chave);
        Assert.Contains("Idempotency-Key", erros.Keys);
    }

    [Fact]
    public void Chave_maior_que_80_caracteres_gera_erro()
    {
        var erros = SinalValidator.Validar(new SinalRequest("TOTEM-0001", "BOTAO", null, Agora), new string('a', 81));
        Assert.Contains("Idempotency-Key", erros.Keys);
    }

    [Fact]
    public void Cartao_sem_uid_gera_erro()
    {
        var erros = SinalValidator.Validar(new SinalRequest("TOTEM-0001", "CARTAO", null, Agora), "k1");
        Assert.Contains("UidCartao", erros.Keys);
    }

    [Theory]
    [InlineData("XYZ")]          // não é hexadecimal
    [InlineData("04A1B2")]       // só 3 bytes
    [InlineData("04A1B2C")]      // número ímpar de dígitos
    public void Uid_em_formato_errado_gera_erro(string uid)
    {
        var erros = SinalValidator.Validar(new SinalRequest("TOTEM-0001", "CARTAO", uid, Agora), "k1");
        Assert.Contains("UidCartao", erros.Keys);
    }

    [Fact]
    public void Uid_com_separadores_e_minusculas_e_aceito()
    {
        var erros = SinalValidator.Validar(new SinalRequest("TOTEM-0001", "CARTAO", "04:a1:b2:c3:d4", Agora), "k1");
        Assert.Empty(erros);
    }

    [Fact]
    public void Botao_com_uid_gera_erro()
    {
        var erros = SinalValidator.Validar(new SinalRequest("TOTEM-0001", "BOTAO", "04A1B2C3D4", Agora), "k1");
        Assert.Contains("UidCartao", erros.Keys);
    }

    [Fact]
    public void Tipo_desconhecido_totem_vazio_e_sem_data_geram_erros()
    {
        var erros = SinalValidator.Validar(new SinalRequest("", "SENSOR", null, null), "k1");
        Assert.Contains("TotemId", erros.Keys);
        Assert.Contains("Tipo", erros.Keys);
        Assert.Contains("DataHora", erros.Keys);
    }
}
