using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParadaCerta.Dispositivos.Api.Configuracao;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Api.Dados;
using ParadaCerta.Dispositivos.Tests.Infra;

namespace ParadaCerta.Dispositivos.Tests.Integracao;

/// <summary>Dependência fora do ar, datas em UTC, validação do login, proxy reverso e documentação OpenAPI.</summary>
public sealed class RobustezApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Banco_fora_do_ar_retorna_503_com_retry_after()
    {
        using var semBanco = api.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.ConfigureDbContext<ParadaCertaDbContext>(o => o.AddInterceptors(new BancoForaDoAr()))));

        var resposta = await semBanco.CreateClient().PostAsJsonAsync("/api/auth/dispositivo",
            new LoginDispositivoRequest(DadosDeTeste.TotemAtivo, DadosDeTeste.SegredoTotem));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(TratadorFalhaDeDependencia.SegundosParaTentarDeNovo), resposta.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Data_do_sinal_sai_em_utc_no_201_e_no_reenvio()
    {
        var cliente = api.CriarClienteDispositivo(DadosDeTeste.TotemAtivo);

        var primeira = await cliente.SendAsync(Sinal("utc-1"));
        var reenvio = await cliente.SendAsync(Sinal("utc-1"));

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reenvio.StatusCode);
        Assert.EndsWith("Z", await RecebidoEm(primeira));
        Assert.Equal(await RecebidoEm(primeira), await RecebidoEm(reenvio));
    }

    [Theory]
    [InlineData("/api/auth/dispositivo", """{"identificador":"","segredo":"x"}""", "identificador")]
    [InlineData("/api/auth/gestor", """{"email":"gestor@paradacerta.test","senha":" "}""", "senha")]
    public async Task Login_com_campo_vazio_retorna_400(string rota, string corpo, string campo)
    {
        var resposta = await api.CreateClient().PostAsync(rota, new StringContent(corpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Fact]
    public async Task Atras_do_proxy_o_limite_de_login_e_por_cliente()
    {
        using var comProxy = ComLimiteDeLogin(proxyHabilitado: true);
        var cliente = comProxy.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await LoginVazio(cliente, "203.0.113.10")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginVazio(cliente, "203.0.113.10")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await LoginVazio(cliente, "203.0.113.20")).StatusCode);
    }

    [Fact]
    public async Task Sem_proxy_o_x_forwarded_for_e_ignorado()
    {
        using var semProxy = ComLimiteDeLogin(proxyHabilitado: false);
        var cliente = semProxy.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await LoginVazio(cliente, "203.0.113.10")).StatusCode);
        // Trocar o cabeçalho não dá uma cota nova: sem proxy, ele não é confiável.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginVazio(cliente, "203.0.113.20")).StatusCode);
    }

    [Theory]
    [InlineData("/api/sinais", "post", new[] { "201", "200", "400", "401", "403", "409", "429", "503" })]
    [InlineData("/api/heartbeats", "post", new[] { "204", "401", "403", "429", "503" })]
    [InlineData("/api/auth/gestor", "post", new[] { "200", "400", "401", "429", "503" })]
    [InlineData("/api/totens", "get", new[] { "200", "400", "401", "403", "503" })]
    [InlineData("/api/totens", "post", new[] { "201", "400", "401", "403", "409", "503" })]
    public async Task OpenApi_documenta_as_respostas_de_erro(string rota, string metodo, string[] esperadas)
    {
        using var documento = JsonDocument.Parse(await api.CreateClient().GetStringAsync("/openapi/v1.json"));
        var respostas = documento.RootElement.GetProperty("paths").GetProperty(rota).GetProperty(metodo).GetProperty("responses");

        var documentadas = respostas.EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Subset(documentadas, esperadas.ToHashSet());
    }

    private static HttpRequestMessage Sinal(string chave)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/sinais")
        {
            Content = JsonContent.Create(new SinalRequest(DadosDeTeste.TotemAtivo, "BOTAO", null, DateTimeOffset.Now))
        };
        requisicao.Headers.Add("Idempotency-Key", chave);
        return requisicao;
    }

    private static async Task<string> RecebidoEm(HttpResponseMessage resposta)
    {
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("recebidoEm").GetString()!;
    }

    private WebApplicationFactory<Program> ComLimiteDeLogin(bool proxyHabilitado) =>
        api.WithWebHostBuilder(b => b.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProxyReverso:Habilitado"] = proxyHabilitado.ToString(),
            ["LimiteRequisicoes:Login:Limite"] = "1"
        })));

    private static Task<HttpResponseMessage> LoginVazio(HttpClient cliente, string ipDoCliente)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/auth/gestor")
        {
            Content = JsonContent.Create(new LoginGestorRequest("", ""))
        };
        requisicao.Headers.Add("X-Forwarded-For", ipDoCliente);
        return cliente.SendAsync(requisicao);
    }

    /// <summary>Simula o Oracle fora do ar: toda consulta falha com timeout de conexão.</summary>
    private sealed class BancoForaDoAr : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct) =>
            throw new TimeoutException("Oracle fora do ar (simulado).");
    }
}
