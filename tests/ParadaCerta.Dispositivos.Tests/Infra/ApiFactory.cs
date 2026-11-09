using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParadaCerta.Dispositivos.Api.Dados;
using ParadaCerta.Dispositivos.Api.Dominio;
using ParadaCerta.Dispositivos.Api.Eventos;
using ParadaCerta.Dispositivos.Api.Mensageria;
using ParadaCerta.Dispositivos.Api.Seguranca;

namespace ParadaCerta.Dispositivos.Tests.Infra;

/// <summary>
/// Sobe a API inteira em memória: Oracle trocado por SQLite, RabbitMQ e MongoDB trocados por fakes.
/// Autenticação, rate limiting, validação e serialização são os reais.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly BancoEmMemoria _banco = new();

    public PublicadorFalso Publicador { get; } = new();
    public RegistroEventosFalso Eventos { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Chave"] = DadosDeTeste.ChaveJwt,
            ["Cartao:ChaveHmac"] = DadosDeTeste.ChaveHmac,
            ["LimiteRequisicoes:Sinais:Limite"] = "10",
            ["LimiteRequisicoes:Sinais:JanelaSegundos"] = "60",
            ["LimiteRequisicoes:Login:Limite"] = "100"
        }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ParadaCertaDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ParadaCertaDbContext>>();
            services.AddDbContext<ParadaCertaDbContext>(o => o.UseSqlite(_banco.Conexao));

            services.RemoveAll<IPublicadorSinais>();
            services.AddSingleton<IPublicadorSinais>(Publicador);
            services.RemoveAll<IRegistroEventos>();
            services.AddSingleton<IRegistroEventos>(Eventos);

            // RabbitMQ e MongoDB não existem no teste: os checks deles ficam de fora.
            services.Configure<HealthCheckServiceOptions>(o =>
            {
                foreach (var nome in new[] { "rabbitmq", "mongodb" })
                {
                    var registro = o.Registrations.FirstOrDefault(r => r.Name == nome);
                    if (registro is not null) o.Registrations.Remove(registro);
                }
            });
        });
    }

    public HttpClient CriarClienteDispositivo(string identificador)
    {
        using var escopo = Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ParadaCertaDbContext>();
        var totem = db.Totens.Single(t => t.Identificador == identificador);
        var token = escopo.ServiceProvider.GetRequiredService<TokenService>().GerarParaDispositivo(totem);
        return ComToken(token.AccessToken);
    }

    public HttpClient CriarClienteGestor()
    {
        var token = Services.GetRequiredService<TokenService>()
            .GerarParaGestor(new Usuario { Id = 1, Nome = "Gestor Teste", Perfil = Perfis.Gestor });
        return ComToken(token.AccessToken);
    }

    public HttpClient ComToken(string token)
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _banco.Dispose();
    }
}
