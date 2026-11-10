using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Conecta la física con el estado del juego. Es dueño del EstadoJuego.
/// Crea una tostada en cada lanzamiento y suma puntos cuando se pega en la pared.
/// </summary>
public class ControladorJuego : MonoBehaviour
{
    [SerializeField] private PalancaTostadora palanca;
    [SerializeField] private Tostada prefabTostada;

    public EstadoJuego Estado { get; private set; } = new EstadoJuego();

    private void OnEnable()
    {
        if (palanca != null) palanca.OnLanzamiento += AlLanzar;
    }

    private void OnDisable()
    {
        if (palanca != null) palanca.OnLanzamiento -= AlLanzar;
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
        Estado.RegistrarImpactoPared();
        Debug.Log($"¡Tostada pegada en la pared! Tostadas: {Estado.tostadas}");
    }

    private void Update()
    {
        // TEMPORAL para pruebas: tecla M compra una mejora del resorte.
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            double costo = Estado.CostoMejoraResorte();
            if (Estado.ComprarMejoraResorte())
                Debug.Log($"Mejora comprada → nivel {Estado.nivelResorte}, k = {Estado.ConstanteResorte():F1} N/m");
            else
                Debug.Log($"Faltan tostadas: la mejora cuesta {costo}, tienes {Estado.tostadas}");
        }
    }
}
