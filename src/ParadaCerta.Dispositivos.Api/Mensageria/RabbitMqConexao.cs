using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ParadaCerta.Dispositivos.Api.Mensageria;

/// <summary>Mantém uma única conexão com o RabbitMQ para toda a aplicação (singleton).</summary>
public sealed class RabbitMqConexao(IOptions<RabbitMqOptions> opcoes) : IAsyncDisposable
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private IConnection? _conexao;

    public async Task<IConnection> ObterAsync(CancellationToken ct)
    {
        if (_conexao is { IsOpen: true }) return _conexao;

        await _trava.WaitAsync(ct);
        try
        {
            if (_conexao is { IsOpen: true }) return _conexao;
            if (_conexao is not null) await _conexao.DisposeAsync();

            _conexao = await CriarFabrica(opcoes.Value).CreateConnectionAsync(ct);
            return _conexao;
        }
        finally
        {
            _trava.Release();
        }
    }

    private static ConnectionFactory CriarFabrica(RabbitMqOptions o)
    {
        var fabrica = new ConnectionFactory
        {
            ClientProvidedName = "parada-certa-api-dispositivos",
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };

        if (!string.IsNullOrWhiteSpace(o.Uri))
        {
            fabrica.Uri = new Uri(o.Uri);
        }
        else
        {
            fabrica.HostName = o.HostName;
            fabrica.Port = o.Porta;
            fabrica.UserName = o.Usuario;
            fabrica.Password = o.Senha;
            fabrica.VirtualHost = o.VirtualHost;
        }

        return fabrica;
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexao is not null) await _conexao.DisposeAsync();
        _trava.Dispose();
    }
}
