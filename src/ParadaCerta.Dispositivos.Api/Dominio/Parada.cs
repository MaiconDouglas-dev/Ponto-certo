namespace ParadaCerta.Dispositivos.Api.Dominio;

/// <summary>Tabela PARADA. A API .NET só lê (cadastro é feito pela API Java).</summary>
public class Parada
{
    public long Id { get; set; }
    public long CodigoSptrans { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Endereco { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
