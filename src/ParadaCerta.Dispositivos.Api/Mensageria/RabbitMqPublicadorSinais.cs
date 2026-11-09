using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ParadaCerta.Dispositivos.Api.Mensageria;

public sealed class RabbitMqPublicadorSinais(RabbitMqConexao conexao, IOptions<RabbitMqOptions> opcoes) : IPublicadorSinais
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task PublicarAsync(MensagemSinalTotem mensagem, CancellationToken ct)
    {
        var fila = opcoes.Value.FilaSinais;
        var conexaoAberta = await conexao.ObterAsync(ct);

        // Com confirmação ativada, BasicPublishAsync só retorna depois do "ack" do broker.
        await using var canal = await conexaoAberta.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);

        // Fila durável: precisa ser declarada com os MESMOS parâmetros no consumidor Java.
        await canal.QueueDeclareAsync(fila, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

        var propriedades = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = mensagem.ChaveIdempotencia,
            Type = "SinalTotemRecebido",
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        var corpo = JsonSerializer.SerializeToUtf8Bytes(mensagem, Json);
        await canal.BasicPublishAsync(string.Empty, fila, mandatory: true, propriedades, corpo, ct);
    }
}
