using System.Net;
using System.Net.Http.Json;
using ParadaCerta.Dispositivos.Api.Contratos;
using ParadaCerta.Dispositivos.Tests.Infra;

namespace ParadaCerta.Dispositivos.Tests.Integracao;

/// <summary>Fluxo do gestor (RF16) e login do totem/gestor.</summary>
public sealed class TotensApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Gestor_cadastra_totem_que_faz_login_e_envia_sinal()
    {
        var gestor = api.CriarClienteGestor();
        var criado = await gestor.PostAsJsonAsync("/api/totens", new CriarTotemRequest("totem-0100", 1));
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var corpo = await criado.Content.ReadFromJsonAsync<TotemComSegredoResponse>();
        Assert.Equal("TOTEM-0100", corpo!.Totem.Identificador);

        var login = await api.CreateClient().PostAsJsonAsync("/api/auth/dispositivo",
            new LoginDispositivoRequest("TOTEM-0100", corpo.Segredo));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = await login.Content.ReadFromJsonAsync<TokenResponse>();

        var totem = api.ComToken(token!.AccessToken);
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/sinais")
        {
            Content = JsonContent.Create(new SinalRequest("TOTEM-0100", "BOTAO", null, DateTimeOffset.Now))
        };
        requisicao.Headers.Add("Idempotency-Key", "TOTEM-0100-000001");
        Assert.Equal(HttpStatusCode.Created, (await totem.SendAsync(requisicao)).StatusCode);
    }

    [Fact]
    public async Task Totem_desativado_pelo_gestor_nao_envia_mais_sinais()
    {
        var gestor = api.CriarClienteGestor();
        var criado = await (await gestor.PostAsJsonAsync("/api/totens", new CriarTotemRequest("TOTEM-0200", 1)))
            .Content.ReadFromJsonAsync<TotemComSegredoResponse>();
        var totem = api.CriarClienteDispositivo("TOTEM-0200");

        var desativar = await gestor.PatchAsJsonAsync($"/api/totens/{criado!.Totem.Id}/status", new AlterarStatusTotemRequest("INATIVO"));
        Assert.Equal(HttpStatusCode.OK, desativar.StatusCode);

        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/sinais")
        {
            Content = JsonContent.Create(new SinalRequest("TOTEM-0200", "BOTAO", null, DateTimeOffset.Now))
        };
        requisicao.Headers.Add("Idempotency-Key", "TOTEM-0200-000001");
        Assert.Equal(HttpStatusCode.Forbidden, (await totem.SendAsync(requisicao)).StatusCode);
    }

    [Fact]
    public async Task Identificador_duplicado_retorna_409()
    {
        var resposta = await api.CriarClienteGestor().PostAsJsonAsync("/api/totens",
            new CriarTotemRequest(DadosDeTeste.TotemAtivo, 1));
        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Parada_inexistente_retorna_400()
    {
        var resposta = await api.CriarClienteGestor().PostAsJsonAsync("/api/totens", new CriarTotemRequest("TOTEM-0300", 999));
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Totem_nao_acessa_a_gestao_de_totens()
    {
        var resposta = await api.CriarClienteDispositivo(DadosDeTeste.TotemAtivo).GetAsync("/api/totens");
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Detalhe_mostra_totem_online_depois_do_heartbeat()
    {
        var totem = api.CriarClienteDispositivo(DadosDeTeste.TotemAtivo);
        var heartbeat = await totem.PostAsJsonAsync("/api/heartbeats", new HeartbeatRequest(-61, "1.0.0", 3600));
        Assert.Equal(HttpStatusCode.NoContent, heartbeat.StatusCode);

        var lista = await api.CriarClienteGestor().GetFromJsonAsync<List<TotemResponse>>("/api/totens?status=ATIVO");
        var id = lista!.Single(t => t.Identificador == DadosDeTeste.TotemAtivo).Id;

        var detalhe = await api.CriarClienteGestor().GetFromJsonAsync<TotemDetalheResponse>($"/api/totens/{id}");
        Assert.True(detalhe!.Online);
        Assert.NotNull(detalhe.UltimoHeartbeatEm);
    }

    [Fact]
    public async Task Login_do_gestor_com_senha_certa_retorna_token()
    {
        var resposta = await api.CreateClient().PostAsJsonAsync("/api/auth/gestor",
            new LoginGestorRequest(DadosDeTeste.EmailGestor.ToUpperInvariant(), DadosDeTeste.SenhaGestor));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Theory]
    [InlineData(DadosDeTeste.EmailGestor, "senha-errada")]
    [InlineData("cuidadora@paradacerta.test", DadosDeTeste.SenhaGestor)] // perfil CUIDADOR não entra
    public async Task Login_do_gestor_invalido_retorna_401(string email, string senha)
    {
        var resposta = await api.CreateClient().PostAsJsonAsync("/api/auth/gestor", new LoginGestorRequest(email, senha));
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_de_totem_inativo_retorna_401()
    {
        var resposta = await api.CreateClient().PostAsJsonAsync("/api/auth/dispositivo",
            new LoginDispositivoRequest(DadosDeTeste.TotemInativo, DadosDeTeste.SegredoTotem));
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
