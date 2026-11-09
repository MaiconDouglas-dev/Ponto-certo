using System.Net;
using System.Net.Http.Json;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Endpoints;
using ParadaCerta.Dispositivos.Tests.Infra;

namespace ParadaCerta.Dispositivos.Tests.Integracao;

/// <summary>Testes de ponta a ponta do POST /api/sinais: 201, 200 repetido, 400, 401, 403, 409 e 429.</summary>
public sealed class SinaisApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 18, 14, 32, 5, TimeSpan.FromHours(-3));

    private static HttpRequestMessage Sinal(string totem, string? chave, string tipo = "CARTAO", string? uid = "04A1B2C3D4")
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/sinais")
        {
            Content = JsonContent.Create(new SinalRequest(totem, tipo, tipo == "CARTAO" ? uid : null, Agora))
        };
        if (chave is not null) requisicao.Headers.Add("Idempotency-Key", chave);
        return requisicao;
    }

    [Fact]
    public async Task Sinal_novo_retorna_201_com_location()
    {
        var cliente = api.CriarClienteDispositivo(DadosDeTeste.TotemAtivo);

        var resposta = await cliente.SendAsync(Sinal(DadosDeTeste.TotemAtivo, "int-201"));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<SinalResponse>();
        Assert.Equal("int-201", corpo!.ChaveIdempotencia);
        Assert.Equal("CARTAO", corpo.Tipo);
        Assert.Equal($"/api/sinais/{corpo.Id}", resposta.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Repetir_a_mesma_chave_retorna_200_com_o_mesmo_sinal()
    {
        var cliente = api.CriarClienteDispositivo(DadosDeTeste.TotemAtivo);

        var primeira = await cliente.SendAsync(Sinal(DadosDeTeste.TotemAtivo, "int-200", "BOTAO"));
        var segunda = await cliente.SendAsync(Sinal(DadosDeTeste.TotemAtivo, "int-200", "BOTAO"));

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.True(segunda.Headers.Contains(SinaisEndpoints.CabecalhoRepeticao));

        var a = await primeira.Content.ReadFromJsonAsync<SinalResponse>();
        var b = await segunda.Content.ReadFromJsonAsync<SinalResponse>();
        Assert.Equal(a!.Id, b!.Id);
        Assert.Single(api.Publicador.Publicadas, m => m.ChaveIdempotencia == "int-200");
    }

    [Fact]
    public async Task Sem_token_retorna_401()
    {
        var resposta = await api.CreateClient().SendAsync(Sinal(DadosDeTeste.TotemAtivo, "int-401"));
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Token_de_gestor_nao_pode_enviar_sinal()
    {
        var resposta = await api.CriarClienteGestor().SendAsync(Sinal(DadosDeTeste.TotemAtivo, "int-403-gestor"));
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Totem_nao_pode_enviar_sinal_em_nome_de_outro()
    {
        var cliente = api.CriarClienteDispositivo(DadosDeTeste.TotemAtivo);
        var resposta = await cliente.SendAsync(Sinal(DadosDeTeste.TotemLimite, "int-403-outro"));
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Sem_idempotency_key_retorna_400()
    {
        var cliente = api.CriarClienteDispositivo(DadosDeTeste.TotemAtivo);
        var resposta = await cliente.SendAsync(Sinal(DadosDeTeste.TotemAtivo, chave: null));
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Chave_reutilizada_com_outro_conteudo_retorna_409()
    {
        var cliente = api.CriarClienteDispositivo(DadosDeTeste.TotemAtivo);

        await cliente.SendAsync(Sinal(DadosDeTeste.TotemAtivo, "int-409", "BOTAO"));
        var resposta = await cliente.SendAsync(Sinal(DadosDeTeste.TotemAtivo, "int-409", "CARTAO"));

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Fila_fora_do_ar_retorna_503()
    {
        var cliente = api.CriarClienteDispositivo(DadosDeTeste.TotemAtivo);
        api.Publicador.Falhar = true;
        try
        {
            var resposta = await cliente.SendAsync(Sinal(DadosDeTeste.TotemAtivo, "int-503"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        }
        finally
        {
            api.Publicador.Falhar = false;
        }
    }

    [Fact]
    public async Task Mais_de_10_sinais_por_minuto_retorna_429_com_retry_after()
    {
        // Totem exclusivo deste teste, para não dividir a cota com os outros.
        var cliente = api.CriarClienteDispositivo(DadosDeTeste.TotemLimite);

        for (var i = 1; i <= 10; i++)
        {
            var ok = await cliente.SendAsync(Sinal(DadosDeTeste.TotemLimite, $"int-429-{i}", "BOTAO"));
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        }

        var bloqueado = await cliente.SendAsync(Sinal(DadosDeTeste.TotemLimite, "int-429-11", "BOTAO"));

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueado.StatusCode);
        Assert.NotNull(bloqueado.Headers.RetryAfter);
        Assert.DoesNotContain(api.Publicador.Publicadas, m => m.ChaveIdempotencia == "int-429-11");
    }
}
