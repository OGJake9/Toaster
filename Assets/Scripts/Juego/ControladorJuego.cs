using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Conecta la física con el estado del juego. Es dueño del EstadoJuego.
/// Crea una tostada en cada lanzamiento, suma puntos cuando se pega en la pared,
/// aplica la producción automática y abre/cierra la tienda (pausando el juego).
/// </summary>
public class ControladorJuego : MonoBehaviour
{
    [SerializeField] private PalancaTostadora palanca;
    [SerializeField] private Tostada prefabTostada;

    public EstadoJuego Estado { get; private set; } = new EstadoJuego();
    public bool TiendaAbierta { get; private set; }

    private void OnEnable()
    {
        if (palanca != null) palanca.OnLanzamiento += AlLanzar;
    }

    private void OnDisable()
    {
        if (palanca != null) palanca.OnLanzamiento -= AlLanzar;
        Time.timeScale = 1f;
    }

    private void Update()
    {
        // Producción automática (con la tienda abierta Time.deltaTime = 0, así que se pausa).
        Estado.Producir(Time.deltaTime);

        if (Keyboard.current == null) return;

        if (Keyboard.current.tKey.wasPressedThisFrame)
            AbrirTienda(!TiendaAbierta);
        else if (TiendaAbierta && Keyboard.current.escapeKey.wasPressedThisFrame)
            AbrirTienda(false);
    }

    public void AbrirTienda(bool abrir)
    {
        TiendaAbierta = abrir;
        Time.timeScale = abrir ? 0f : 1f;              // pausa toda la física
        if (palanca != null) palanca.EntradaHabilitada = !abrir;
    }

    public void ComprarMejora(TipoMejora tipo)
    {
        if (Estado.Comprar(tipo))
            Debug.Log($"Comprado: {EstadoJuego.Mejoras[(int)tipo].nombre} → nivel {Estado.Nivel(tipo)}");
    }

    private void AlLanzar(Vector2 posicion, Vector2 velocidad)
    {
        Estado.RegistrarLanzamiento();

        // Comparación teoría vs. simulación del resorte.
        // Sin amortiguamiento:  ½·k·x0² = ½·m·v²   →   v = x0·√(k/m)
        float k = Estado.ConstanteResorte();
        float x0 = palanca.CompresionAlSoltar;
        float vTeorica = x0 * Mathf.Sqrt(k / palanca.Masa);

        Debug.Log($"Lanzamiento #{Estado.lanzamientos} | k = {k:F1} N/m | x0 = {x0:F3} m | " +
                  $"v simulada = {velocidad.magnitude:F2} m/s | v teórica (sin c) = {vTeorica:F2} m/s");

        if (prefabTostada != null)
        {
            Tostada t = Instantiate(prefabTostada);
            t.Iniciar(posicion, velocidad, AlPegarseEnPared);
        }
    }

    private void AlPegarseEnPared(Tostada tostada)
    {
        int ganadas = Estado.RegistrarImpactoPared();
        Debug.Log($"¡Impacto! +{ganadas} | Tostadas: {Estado.tostadas:0}");
    }
}
