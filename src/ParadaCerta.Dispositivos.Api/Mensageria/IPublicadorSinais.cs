namespace ParadaCerta.Dispositivos.Api.Mensageria;

public interface IPublicadorSinais
{
    /// <summary>Publica e só retorna depois da confirmação do broker. Lança exceção se falhar.</summary>
    Task PublicarAsync(MensagemSinalTotem mensagem, CancellationToken ct);
}
