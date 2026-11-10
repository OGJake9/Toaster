using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Conecta la física con el estado del juego. Es dueño del EstadoJuego.
/// Por ahora solo registra lanzamientos y escribe datos en la consola para
/// verificar la física; la UI y la tostada voladora llegan en las siguientes fases.
/// </summary>
public class ControladorJuego : MonoBehaviour
{
    [SerializeField] private PalancaTostadora palanca;

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

        // Comparación teoría vs. simulación.
        // Sin amortiguamiento, la energía elástica se convierte en cinética:
        //   ½·k·x0² = ½·m·v²   →   v = x0·√(k/m)
        float k = Estado.ConstanteResorte();
        float x0 = palanca.CompresionAlSoltar;
        float vTeorica = x0 * Mathf.Sqrt(k / palanca.Masa);

        Debug.Log($"Lanzamiento #{Estado.lanzamientos} | k = {k:F1} N/m | x0 = {x0:F3} m | " +
                  $"v simulada = {velocidad.magnitude:F2} m/s | v teórica (sin c) = {vTeorica:F2} m/s | " +
                  $"tostadas = {Estado.tostadas}");
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
