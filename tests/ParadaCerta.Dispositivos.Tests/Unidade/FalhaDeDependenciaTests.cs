using System.Net.Sockets;
using ParadaCerta.Dispositivos.Api.Configuracao;

namespace ParadaCerta.Dispositivos.Tests.Unidade;

public sealed class FalhaDeDependenciaTests
{
    [Fact]
    public void Timeout_e_falha_de_rede_sao_falha_de_dependencia()
    {
        Assert.True(FalhaDeDependencia.Detectar(new TimeoutException()));
        Assert.True(FalhaDeDependencia.Detectar(new SocketException((int)SocketError.ConnectionRefused)));
    }

    [Fact]
    public void Reconhece_a_falha_embrulhada_pelo_driver()
    {
        var embrulhada = new InvalidOperationException("erro do EF", new TimeoutException("conexão"));
        var agregada = new AggregateException(new ArgumentException(), new SocketException());

        Assert.True(FalhaDeDependencia.Detectar(embrulhada));
        Assert.True(FalhaDeDependencia.Detectar(agregada));
    }

    [Fact]
    public void Erro_de_programacao_continua_sendo_500()
    {
        Assert.False(FalhaDeDependencia.Detectar(new NullReferenceException()));
        Assert.False(FalhaDeDependencia.Detectar(new InvalidOperationException("regra")));
        Assert.False(FalhaDeDependencia.Detectar(null));
    }
}
