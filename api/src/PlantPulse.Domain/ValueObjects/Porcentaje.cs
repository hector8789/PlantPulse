using PlantPulse.Domain.Common;

namespace PlantPulse.Domain.ValueObjects;

/// <summary>
/// Fracción entre 0 y 1 (0.85 = 85%). Se usa para Disponibilidad, Desempeño, Calidad, OEE y tasa de scrap.
/// Nunca contiene NaN ni infinito: los decimales no los representan y cada división define su caso de cero.
/// </summary>
public sealed record Porcentaje : IComparable<Porcentaje>
{
    public static class Errores
    {
        public const string FueraDeRango = "porcentaje.fuera_de_rango";
        public const string Negativo = "porcentaje.valor_negativo";
    }

    public static readonly Porcentaje Cero = new(0m);
    public static readonly Porcentaje Cien = new(1m);

    public decimal Valor { get; }

    private Porcentaje(decimal valor) => Valor = valor;

    public static Porcentaje Crear(decimal valor)
    {
        if (valor < 0m || valor > 1m)
            throw new DomainException(Errores.FueraDeRango, $"El porcentaje debe estar entre 0 y 1; se recibió {valor}.");
        return new Porcentaje(valor);
    }

    /// <summary>
    /// numerador / denominador. Si el denominador es 0 devuelve <paramref name="siDenominadorCero"/>.
    /// Lanza si la razón resulta mayor a 1: para valores que pueden excederlo usa <see cref="DeRazonLimitada"/>.
    /// </summary>
    public static Porcentaje DeRazon(decimal numerador, decimal denominador, Porcentaje siDenominadorCero)
    {
        ValidarNoNegativos(numerador, denominador);
        return denominador == 0m ? siDenominadorCero : Crear(numerador / denominador);
    }

    /// <summary>
    /// Igual que <see cref="DeRazon"/> pero limita el resultado a 100%.
    /// <paramref name="razonCalculada"/> conserva el valor sin limitar para registrar la advertencia.
    /// </summary>
    public static Porcentaje DeRazonLimitada(
        decimal numerador, decimal denominador, Porcentaje siDenominadorCero, out decimal razonCalculada)
    {
        ValidarNoNegativos(numerador, denominador);
        if (denominador == 0m)
        {
            razonCalculada = siDenominadorCero.Valor;
            return siDenominadorCero;
        }
        razonCalculada = numerador / denominador;
        return razonCalculada > 1m ? Cien : new Porcentaje(razonCalculada);
    }

    public Porcentaje Por(Porcentaje otro) => new(Valor * otro.Valor);

    /// <summary>Valor en escala 0–100, redondeado para mostrar (85.27).</summary>
    public decimal EnEscala100(int decimales = 2) =>
        Math.Round(Valor * 100m, decimales, MidpointRounding.AwayFromZero);

    public int CompareTo(Porcentaje? other) => other is null ? 1 : Valor.CompareTo(other.Valor);

    public override string ToString() => $"{EnEscala100()}%";

    private static void ValidarNoNegativos(decimal numerador, decimal denominador)
    {
        if (numerador < 0m || denominador < 0m)
            throw new DomainException(Errores.Negativo, "Numerador y denominador no pueden ser negativos.");
    }
}
