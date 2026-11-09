using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Dados;
using ParadaCerta.Dispositivos.Api.Eventos;
using ParadaCerta.Dispositivos.Api.Seguranca;
using ParadaCerta.Dispositivos.Api.Servicos;
using ParadaCerta.Dispositivos.Tests.Infra;

namespace ParadaCerta.Dispositivos.Tests.Unidade;

/// <summary>Regras do sinal e da idempotência, com SQLite em memória e fila falsa.</summary>
public sealed class SinalServiceTests : IDisposable
{
    private readonly BancoEmMemoria _banco = new();
    private readonly ParadaCertaDbContext _db;
    private readonly PublicadorFalso _publicador = new();
    private readonly RegistroEventosFalso _eventos = new();
    private readonly SinalService _servico;

    private static readonly DateTimeOffset Agora = new(2026, 11, 18, 17, 32, 5, TimeSpan.Zero);

    public SinalServiceTests()
    {
        _db = _banco.CriarContexto();
        _servico = new SinalService(
            _db,
            new HashCartao(Options.Create(new CartaoOptions { ChaveHmac = DadosDeTeste.ChaveHmac })),
            _publicador,
            _eventos,
            new FakeTimeProvider(Agora),
            NullLogger<SinalService>.Instance);
    }

    private static SinalRequest Cartao(string totem = DadosDeTeste.TotemAtivo, string uid = "04A1B2C3D4") =>
        new(totem, "CARTAO", uid, Agora);

    [Fact]
    public async Task Sinal_novo_e_gravado_e_publicado()
    {
        var resultado = await _servico.ReceberAsync(Cartao(), "chave-1", default);

        Assert.Equal(ResultadoSinalTipo.Criado, resultado.Tipo);
        var gravado = await _db.Sinais.SingleAsync(s => s.ChaveIdempotencia == "chave-1");
        Assert.Matches("^[0-9a-f]{64}$", gravado.UidHash);

        var mensagem = Assert.Single(_publicador.Publicadas);
        Assert.Equal(gravado.Id, mensagem.SinalId);
        Assert.Equal("CARTAO", mensagem.Tipo);
        Assert.Equal(gravado.UidHash, mensagem.UidHash);
        Assert.Contains(_eventos.Eventos, e => e.Tipo == TiposEvento.SinalRecebido);
    }

    [Fact]
    public async Task Uid_puro_do_cartao_nunca_e_gravado_nem_publicado()
    {
        await _servico.ReceberAsync(Cartao(uid: "04A1B2C3D4"), "chave-uid", default);

        var gravado = await _db.Sinais.SingleAsync(s => s.ChaveIdempotencia == "chave-uid");
        Assert.DoesNotContain("04A1B2C3D4", gravado.UidHash!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("04A1B2C3D4", _publicador.Publicadas.Single().UidHash!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mesma_chave_devolve_o_mesmo_sinal_sem_duplicar()
    {
        var primeiro = await _servico.ReceberAsync(Cartao(), "chave-2", default);
        var segundo = await _servico.ReceberAsync(Cartao(), "chave-2", default);

        Assert.Equal(ResultadoSinalTipo.Repetido, segundo.Tipo);
        Assert.Equal(primeiro.Sinal!.Id, segundo.Sinal!.Id);
        Assert.Equal(1, await _db.Sinais.CountAsync(s => s.ChaveIdempotencia == "chave-2"));
        Assert.Single(_publicador.Publicadas);
    }

    [Fact]
    public async Task Mesma_chave_com_outro_cartao_e_conflito()
    {
        await _servico.ReceberAsync(Cartao(uid: "04A1B2C3D4"), "chave-3", default);
        var resultado = await _servico.ReceberAsync(Cartao(uid: "0A0B0C0D"), "chave-3", default);

        Assert.Equal(ResultadoSinalTipo.ChaveEmConflito, resultado.Tipo);
    }

    [Fact]
    public async Task Mesma_chave_vinda_de_outro_totem_e_conflito()
    {
        await _servico.ReceberAsync(Cartao(DadosDeTeste.TotemAtivo), "chave-4", default);
        var resultado = await _servico.ReceberAsync(Cartao(DadosDeTeste.TotemLimite), "chave-4", default);

        Assert.Equal(ResultadoSinalTipo.ChaveEmConflito, resultado.Tipo);
    }

    [Fact]
    public async Task Totem_inativo_nao_grava_sinal()
    {
        var resultado = await _servico.ReceberAsync(Cartao(DadosDeTeste.TotemInativo), "chave-5", default);

        Assert.Equal(ResultadoSinalTipo.TotemInativo, resultado.Tipo);
        Assert.False(await _db.Sinais.AnyAsync(s => s.ChaveIdempotencia == "chave-5"));
        Assert.Empty(_publicador.Publicadas);
    }

    [Fact]
    public async Task Falha_na_fila_desfaz_o_registro_e_permite_reenviar_com_a_mesma_chave()
    {
        _publicador.Falhar = true;
        var falhou = await _servico.ReceberAsync(Cartao(), "chave-6", default);

        Assert.Equal(ResultadoSinalTipo.MensageriaIndisponivel, falhou.Tipo);
        Assert.False(await _db.Sinais.AnyAsync(s => s.ChaveIdempotencia == "chave-6"));

        _publicador.Falhar = false;
        var reenvio = await _servico.ReceberAsync(Cartao(), "chave-6", default);

        Assert.Equal(ResultadoSinalTipo.Criado, reenvio.Tipo);
        Assert.Single(_publicador.Publicadas);
    }

    [Fact]
    public async Task Sinal_atualiza_ultimo_sinal_do_totem()
    {
        await _servico.ReceberAsync(Cartao(), "chave-7", default);

        var totem = await _db.Totens.AsNoTracking().SingleAsync(t => t.Identificador == DadosDeTeste.TotemAtivo);
        Assert.Equal(Agora.UtcDateTime, totem.UltimoSinalEm);
    }

    public void Dispose()
    {
        _db.Dispose();
        _banco.Dispose();
    }
}
