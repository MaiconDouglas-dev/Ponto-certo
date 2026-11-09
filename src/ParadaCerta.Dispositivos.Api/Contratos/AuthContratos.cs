namespace ParadaCerta.Dispositivos.Api.Contratos;

/// <summary>Credenciais gravadas no firmware do totem.</summary>
public sealed record LoginDispositivoRequest(string Identificador, string Segredo);

public sealed record LoginGestorRequest(string Email, string Senha);

public sealed record TokenResponse(string AccessToken, string TokenType, DateTimeOffset ExpiraEm, string Perfil);
