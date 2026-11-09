using System.Net;
using System.Text.Json;
using ParadaCerta.Dispositivos.Tests.Infra;

namespace ParadaCerta.Dispositivos.Tests.Integracao;

public sealed class SaudeApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_checks_respondem_healthy(string rota)
    {
        var resposta = await api.CreateClient().GetAsync(rota);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Documento_openapi_esta_disponivel()
    {
        var resposta = await api.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("/api/sinais", await resposta.Content.ReadAsStringAsync());
    }
}
