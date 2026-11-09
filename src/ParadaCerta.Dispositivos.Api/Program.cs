using ParadaCerta.Dispositivos.Api.Configuracao;
using ParadaCerta.Dispositivos.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiBase()
    .AddProxyReverso()
    .AddDados()
    .AddMensageria()
    .AddEventos()
    .AddSeguranca()
    .AddLimiteDeRequisicoes()
    .AddVerificacoesDeSaude()
    .AddServicos();

var app = builder.Build();

// Precisa vir antes de tudo que lê o IP do cliente (rate limiter, logs).
if (app.Configuration.GetValue<bool>("ProxyReverso:Habilitado"))
    app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "Parada Certa - API Dispositivos v1");
    o.DocumentTitle = "Parada Certa - API Dispositivos";
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapAuthEndpoints();
app.MapSinaisEndpoints();
app.MapHeartbeatEndpoints();
app.MapTotensEndpoints();
app.MapVerificacoesDeSaude();

app.Run();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
