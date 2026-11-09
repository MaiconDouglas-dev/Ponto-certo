namespace ParadaCerta.Dispositivos.Api.Seguranca;

public static class Perfis
{
    public const string Dispositivo = "DISPOSITIVO";
    public const string Gestor = "GESTOR";
}

public static class Politicas
{
    public const string Dispositivo = "SomenteDispositivo";
    public const string Gestor = "SomenteGestor";
}

public static class ClaimsParadaCerta
{
    public const string Perfil = "role";
    public const string TotemId = "totem_id";
}
