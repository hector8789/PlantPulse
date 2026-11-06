using PlantPulse.Domain.Common;

namespace PlantPulse.Domain.ValueObjects;

/// <summary>Lapso de tiempo no negativo (duración de turno, de paro, tiempo operativo).</summary>
public sealed record Duracion : IComparable<Duracion>
{
    public static class Errores
    {
        public const string Negativa = "duracion.negativa";
    }

    public static readonly Duracion Cero = new(TimeSpan.Zero);

    public TimeSpan Valor { get; }

    private Duracion(TimeSpan valor) => Valor = valor;

    public static Duracion De(TimeSpan valor)
    {
        if (valor < TimeSpan.Zero)
            throw new DomainException(Errores.Negativa, $"Una duración no puede ser negativa ({valor}).");
        return new Duracion(valor);
    }

    public static Duracion DeMinutos(double minutos) => De(TimeSpan.FromMinutes(minutos));

    public static Duracion Entre(DateTimeOffset inicio, DateTimeOffset fin) => De(fin - inicio);

    /// <summary>Segundos exactos (sin pasar por double).</summary>
    public decimal TotalSegundos => (decimal)Valor.Ticks / TimeSpan.TicksPerSecond;

    public decimal TotalMinutos => TotalSegundos / 60m;

    public static Duracion operator +(Duracion a, Duracion b) => new(a.Valor + b.Valor);

    /// <summary>Resta; lanza si el resultado sería negativo.</summary>
    public static Duracion operator -(Duracion a, Duracion b) => De(a.Valor - b.Valor);

    public static bool operator >(Duracion a, Duracion b) => a.Valor > b.Valor;
    public static bool operator <(Duracion a, Duracion b) => a.Valor < b.Valor;
    public static bool operator >=(Duracion a, Duracion b) => a.Valor >= b.Valor;
    public static bool operator <=(Duracion a, Duracion b) => a.Valor <= b.Valor;

    public static Duracion Suma(IEnumerable<Duracion> duraciones) =>
        duraciones.Aggregate(Cero, (acc, d) => acc + d);

    public int CompareTo(Duracion? other) => other is null ? 1 : Valor.CompareTo(other.Valor);

    public override string ToString() => $"{TotalMinutos:0.##} min";
}
