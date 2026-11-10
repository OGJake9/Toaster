using System;
using UnityEngine;

public enum TipoMejora { Resorte = 0, Mantequilla = 1, Recarga = 2, Ayudante = 3 }

/// <summary>Datos fijos de una mejora de la tienda.</summary>
public class InfoMejora
{
    public readonly string nombre;
    public readonly double costoBase;
    public readonly double factorCosto;
    public readonly int nivelMaximo;

    public InfoMejora(string nombre, double costoBase, double factorCosto, int nivelMaximo)
    {
        this.nombre = nombre;
        this.costoBase = costoBase;
        this.factorCosto = factorCosto;
        this.nivelMaximo = nivelMaximo;
    }
}

/// <summary>
/// Estado puro del juego. NO es MonoBehaviour: solo datos y reglas de la economía.
/// Es serializable a JSON para guardar la partida y, más adelante, para que el
/// backend pueda recalcular y validar el progreso (anti-trampa).
/// </summary>
[Serializable]
public class EstadoJuego
{
    // ---------- Datos guardables ----------
    public double tostadas;              // moneda actual
    public double tostadasTotales;       // histórico acumulado (para ranking)
    public long lanzamientos;
    public long impactos;
    public int[] niveles = new int[4];   // nivel de cada mejora, indexado por TipoMejora

    // ---------- Catálogo de la tienda (constante, igual en cliente y servidor) ----------
    public static readonly InfoMejora[] Mejoras =
    {
        new InfoMejora("Resorte reforzado",  10, 1.6, 8),
        new InfoMejora("Mantequilla extra",  15, 1.7, 20),
        new InfoMejora("Recarga rápida",     20, 1.8, 8),
        new InfoMejora("Ayudante de cocina", 25, 1.5, 30),
    };

    // ---------- Parámetros de balance ----------
    public const float K_BASE = 80f;                // constante del resorte base [N/m]
    public const float K_FACTOR_NIVEL = 1.25f;      // cada nivel multiplica k
    public const float RECARGA_BASE = 0.8f;         // [s]
    public const float RECARGA_FACTOR = 0.85f;      // cada nivel multiplica el tiempo
    public const double PRODUCCION_POR_AYUDANTE = 0.5; // [tostadas/s] por nivel

    // ---------- Niveles y compras ----------

    public int Nivel(TipoMejora t)
    {
        if (niveles == null || niveles.Length < Mejoras.Length)
            Array.Resize(ref niveles, Mejoras.Length);
        return niveles[(int)t];
    }

    public bool EstaAlMaximo(TipoMejora t) => Nivel(t) >= Mejoras[(int)t].nivelMaximo;

    public double Costo(TipoMejora t)
    {
        InfoMejora info = Mejoras[(int)t];
        return Math.Floor(info.costoBase * Math.Pow(info.factorCosto, Nivel(t)));
    }

    public bool PuedeComprar(TipoMejora t) => !EstaAlMaximo(t) && tostadas >= Costo(t);

    public bool Comprar(TipoMejora t)
    {
        if (!PuedeComprar(t)) return false;
        tostadas -= Costo(t);
        niveles[(int)t]++;
        return true;
    }

    // ---------- Efectos de las mejoras ----------

    private static float KConNivel(int n) => K_BASE * Mathf.Pow(K_FACTOR_NIVEL, n);
    private static int PorImpactoConNivel(int n) => 1 + n;
    private static float RecargaConNivel(int n) => RECARGA_BASE * Mathf.Pow(RECARGA_FACTOR, n);
    private static double ProduccionConNivel(int n) => n * PRODUCCION_POR_AYUDANTE;

    public float ConstanteResorte() => KConNivel(Nivel(TipoMejora.Resorte));
    public int TostadasPorImpacto() => PorImpactoConNivel(Nivel(TipoMejora.Mantequilla));
    public float TiempoRecarga() => RecargaConNivel(Nivel(TipoMejora.Recarga));
    public double ProduccionPorSegundo() => ProduccionConNivel(Nivel(TipoMejora.Ayudante));

    /// <summary>Texto "actual → siguiente" para mostrar en la tienda.</summary>
    public string DescribirEfecto(TipoMejora t)
    {
        int n = Nivel(t);
        bool max = EstaAlMaximo(t);
        switch (t)
        {
            case TipoMejora.Resorte:
                return max ? $"k = {KConNivel(n):0} N/m"
                           : $"k: {KConNivel(n):0} → {KConNivel(n + 1):0} N/m";
            case TipoMejora.Mantequilla:
                return max ? $"{PorImpactoConNivel(n)} tostadas por impacto"
                           : $"Por impacto: {PorImpactoConNivel(n)} → {PorImpactoConNivel(n + 1)} tostadas";
            case TipoMejora.Recarga:
                return max ? $"Recarga: {RecargaConNivel(n):0.00} s"
                           : $"Recarga: {RecargaConNivel(n):0.00} → {RecargaConNivel(n + 1):0.00} s";
            case TipoMejora.Ayudante:
                return max ? $"{ProduccionConNivel(n):0.#} tostadas/s"
                           : $"Automático: {ProduccionConNivel(n):0.#} → {ProduccionConNivel(n + 1):0.#} tostadas/s";
        }
        return "";
    }

    // ---------- Eventos del juego ----------

    public void RegistrarLanzamiento()
    {
        lanzamientos++;
    }

    /// <summary>Una tostada se pegó en la pared. Devuelve cuántas tostadas ganó.</summary>
    public int RegistrarImpactoPared()
    {
        int ganadas = TostadasPorImpacto();
        impactos++;
        tostadas += ganadas;
        tostadasTotales += ganadas;
        return ganadas;
    }

    /// <summary>Producción automática del ayudante durante dt segundos.</summary>
    public void Producir(float dt)
    {
        double producido = ProduccionPorSegundo() * dt;
        tostadas += producido;
        tostadasTotales += producido;
    }

    // ---------- Serialización ----------
    public string AJson() => JsonUtility.ToJson(this);
    public static EstadoJuego DesdeJson(string json) => JsonUtility.FromJson<EstadoJuego>(json);
}
