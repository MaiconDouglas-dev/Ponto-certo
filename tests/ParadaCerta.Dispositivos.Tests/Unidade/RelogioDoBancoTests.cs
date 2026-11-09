using Microsoft.Extensions.Time.Testing;
using ParadaCerta.Dispositivos.Api.Dados;

namespace ParadaCerta.Dispositivos.Tests.Unidade;

public sealed class RelogioDoBancoTests
{
    [Fact]
    public void Corta_em_microssegundos_como_o_timestamp_do_oracle()
    {
        // 2026-11-09 20:35:35.4554889 UTC: 7 casas decimais, uma a mais do que o Oracle guarda.
        var relogio = new FakeTimeProvider(new DateTimeOffset(2026, 11, 9, 20, 35, 35, TimeSpan.Zero).AddTicks(4_554_889));

        var agora = relogio.AgoraParaOBanco();

        Assert.Equal(DateTimeKind.Utc, agora.Kind);
        Assert.Equal("2026-11-09T20:35:35.455488Z", agora.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ"));
    }
}
