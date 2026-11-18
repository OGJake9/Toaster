/// <summary>
/// Datos de la sesión actual del jugador. Estático porque hay una sola sesión a la vez.
/// La autenticación real la lleva una cookie httpOnly que maneja el navegador
/// (o el caché de cookies de UnityWebRequest en el Editor): el juego NUNCA guarda
/// ni ve el token, así que un script malicioso no lo puede robar.
/// </summary>
public static class Sesion
{
    public static string Usuario { get; private set; }

    /// <summary>true = sesión con el servidor (el progreso se guarda en MySQL).</summary>
    public static bool EnLinea { get; private set; }

    public static void IniciarEnLinea(string usuario)
    {
        Usuario = usuario;
        EnLinea = true;
    }

    public static void IniciarInvitado()
    {
        Usuario = "Invitado";
        EnLinea = false;
    }

    public static void Cerrar()
    {
        Usuario = null;
        EnLinea = false;
    }
}
