using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Oracle.EntityFrameworkCore.Infrastructure;
using ParadaCerta.Dispositivos.Api.Dados;
using ParadaCerta.Dispositivos.Api.Eventos;
using ParadaCerta.Dispositivos.Api.Mensageria;
using ParadaCerta.Dispositivos.Api.Saude;
using ParadaCerta.Dispositivos.Api.Seguranca;
using ParadaCerta.Dispositivos.Api.Servicos;

namespace ParadaCerta.Dispositivos.Api.Configuracao;

/// <summary>
/// Registro de dependências, separado por assunto para o Program.cs ficar curto.
/// As configurações são lidas de forma tardia (IOptions), o que permite aos testes trocá-las.
/// </summary>
public static class ServicosExtensions
{
    public static IServiceCollection AddApiBase(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<TratadorFalhaDeDependencia>();
        services.AddOpenApi(o => o.AddDocumentTransformer<OpenApiBearer>());
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        });
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    /// <summary>
    /// Atrás do proxy da nuvem, o IP da conexão é o do proxy. Com ProxyReverso:Habilitado=true, o IP do cliente
    /// passa a vir do X-Forwarded-For, e o rate limit de login volta a ser por cliente.
    /// ForwardLimit = 1 usa só o valor acrescentado pelo proxy (o último), que o cliente não consegue forjar.
    /// Deixe desligado quando a API recebe conexões direto, senão o cliente escolhe o próprio IP.
    /// </summary>
    public static IServiceCollection AddProxyReverso(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            // O IP do proxy da plataforma muda e não é conhecido de antemão.
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });
        return services;
    }

    public static IServiceCollection AddDados(this IServiceCollection services)
    {
        services.AddDbContext<ParadaCertaDbContext>((sp, o) =>
        {
            var conexao = sp.GetRequiredService<IConfiguration>().GetConnectionString("Oracle");
            if (string.IsNullOrWhiteSpace(conexao))
                throw new InvalidOperationException("Configure ConnectionStrings:Oracle (veja o README).");

            // 19 = versão do Oracle da FIAP; também funciona no Oracle Free 23ai do docker-compose.
            o.UseOracle(conexao, ora => ora.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
        });
        return services;
    }

    public static IServiceCollection AddMensageria(this IServiceCollection services)
    {
        services.AddOptions<RabbitMqOptions>().BindConfiguration(RabbitMqOptions.Secao);
        services.AddSingleton<RabbitMqConexao>();
        services.AddSingleton<IPublicadorSinais, RabbitMqPublicadorSinais>();
        return services;
    }

    public static IServiceCollection AddEventos(this IServiceCollection services)
    {
        services.AddOptions<MongoOptions>().BindConfiguration(MongoOptions.Secao);
        services.AddSingleton<IMongoClient>(sp =>
        {
            var conexao = sp.GetRequiredService<IConfiguration>().GetConnectionString("MongoDb");
            if (string.IsNullOrWhiteSpace(conexao))
                throw new InvalidOperationException("Configure ConnectionStrings:MongoDb (veja o README).");

            var configuracao = MongoClientSettings.FromConnectionString(conexao);
            configuracao.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            return new MongoClient(configuracao);
        });
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>()
            .GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Banco));
        services.AddSingleton<IRegistroEventos, MongoRegistroEventos>();
        return services;
    }

    public static IServiceCollection AddSeguranca(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.Secao).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<CartaoOptions>().BindConfiguration(CartaoOptions.Secao).ValidateDataAnnotations().ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Emissor,
                    ValidAudience = jwt.Value.Audiencia,
                    IssuerSigningKey = TokenService.CriarChave(jwt.Value.Chave),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = ClaimsParadaCerta.Perfil,
                    NameClaimType = "sub"
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.Dispositivo, p => p.RequireRole(Perfis.Dispositivo).RequireClaim(ClaimsParadaCerta.TotemId))
            .AddPolicy(Politicas.Gestor, p => p.RequireRole(Perfis.Gestor));

        services.AddSingleton<IHashCartao, HashCartao>();
        services.AddSingleton<TokenService>();
        return services;
    }

    public static IServiceCollection AddServicos(this IServiceCollection services)
    {
        services.AddScoped<SinalService>();
        services.AddScoped<TotemService>();
        services.AddScoped<AuthService>();
        return services;
    }

    public static IServiceCollection AddVerificacoesDeSaude(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("api", () => HealthCheckResult.Healthy("API respondendo."), tags: [TagsSaude.Vivo])
            .AddDbContextCheck<ParadaCertaDbContext>("oracle", tags: [TagsSaude.Pronto])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [TagsSaude.Pronto], timeout: TimeSpan.FromSeconds(5))
            .AddCheck<MongoHealthCheck>("mongodb", tags: [TagsSaude.Pronto], timeout: TimeSpan.FromSeconds(5));
        return services;
    }

    /// <summary>/health (tudo), /health/live (só a API) e /health/ready (dependências externas).</summary>
    public static IEndpointRouteBuilder MapVerificacoesDeSaude(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = RespostaSaude.EscreverAsync });
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains(TagsSaude.Vivo),
            ResponseWriter = RespostaSaude.EscreverAsync
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains(TagsSaude.Pronto),
            ResponseWriter = RespostaSaude.EscreverAsync
        });
        return app;
    }
}

public static class TagsSaude
{
    public const string Vivo = "live";
    public const string Pronto = "ready";
}
