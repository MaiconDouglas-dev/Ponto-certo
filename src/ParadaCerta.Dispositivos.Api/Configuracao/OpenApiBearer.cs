using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ParadaCerta.Dispositivos.Api.Configuracao;

/// <summary>Adiciona o esquema Bearer (JWT) ao documento OpenAPI, habilitando o botão "Authorize" no Swagger.</summary>
internal sealed class OpenApiBearer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Parada Certa - API Dispositivos",
            Version = "v1",
            Description = "Recebe os sinais dos totens ESP32 (cartão ou botão) e publica na fila sinal-totem para a API Java."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Cole só o token (sem a palavra Bearer). Obtenha em /api/auth/dispositivo ou /api/auth/gestor."
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

        return Task.CompletedTask;
    }
}
