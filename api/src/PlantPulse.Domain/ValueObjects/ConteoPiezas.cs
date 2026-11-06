using PlantPulse.Domain.Common;

namespace PlantPulse.Domain.ValueObjects;

/// <summary>
/// Piezas totales y buenas de una captura. Scrap = totales − buenas; Calidad = buenas / totales.
/// Invariantes: ambos ≥ 0 y buenas ≤ totales.
/// </summary>
public sealed record ConteoPiezas
{
    public static class Errores
    {
        public const string Invalido = "conteo_piezas.invalido";
    }

    public static readonly ConteoPiezas Vacio = new(0, 0);

    public int Totales { get; }
    public int Buenas { get; }

    private ConteoPiezas(int totales, int buenas)
    {
        Totales = totales;
        Buenas = buenas;
    }

    public static ConteoPiezas Crear(int totales, int buenas)
    {
        if (totales < 0 || buenas < 0)
            throw new DomainException(Errores.Invalido, "Las piezas no pueden ser negativas.");
        if (buenas > totales)
            throw new DomainException(Errores.Invalido,
                $"Las piezas buenas ({buenas}) no pueden ser más que las totales ({totales}).");
        return new ConteoPiezas(totales, buenas);
    }

    public int Scrap => Totales - Buenas;

    /// <summary>Sin piezas producidas la calidad se define como 0% (no hay evidencia de piezas buenas).</summary>
    public Porcentaje Calidad => Porcentaje.DeRazon(Buenas, Totales, Porcentaje.Cero);

    /// <summary>Sin piezas producidas la tasa de scrap es 0%.</summary>
    public Porcentaje TasaScrap => Porcentaje.DeRazon(Scrap, Totales, Porcentaje.Cero);

    public static ConteoPiezas operator +(ConteoPiezas a, ConteoPiezas b) =>
        new(checked(a.Totales + b.Totales), checked(a.Buenas + b.Buenas));
}
