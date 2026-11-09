namespace ParadaCerta.Dispositivos.Api.Dados;

public static class RelogioDoBanco
{
    /// <summary>
    /// Agora em UTC, cortado em microssegundos: a precisão do TIMESTAMP do Oracle (6 casas).
    /// Sem isso, a resposta do 201 sai com 7 casas e o reenvio (lido do banco) com 6, e o mesmo sinal parece diferente.
    /// </summary>
    public static DateTime AgoraParaOBanco(this TimeProvider relogio)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        return agora.AddTicks(-(agora.Ticks % (TimeSpan.TicksPerMillisecond / 1000)));
    }
}
