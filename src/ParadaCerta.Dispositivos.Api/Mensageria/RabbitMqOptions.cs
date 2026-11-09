namespace ParadaCerta.Dispositivos.Api.Mensageria;

public sealed class RabbitMqOptions
{
    public const string Secao = "RabbitMq";

    /// <summary>Se preenchida (ex.: amqps://usuario:senha@host/vhost), tem prioridade sobre os campos abaixo.</summary>
    public string? Uri { get; set; }

    public string HostName { get; set; } = "localhost";
    public int Porta { get; set; } = 5672;
    public string Usuario { get; set; } = "guest";
    public string Senha { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    /// <summary>Fila consumida pela API Java.</summary>
    public string FilaSinais { get; set; } = "sinal-totem";
}
