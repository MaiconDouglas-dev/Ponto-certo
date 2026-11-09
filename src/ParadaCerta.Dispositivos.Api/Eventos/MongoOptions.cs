namespace ParadaCerta.Dispositivos.Api.Eventos;

public sealed class MongoOptions
{
    public const string Secao = "MongoDb";

    public string Banco { get; set; } = "parada_certa";
    public string ColecaoEventos { get; set; } = "eventos_totem";
}
