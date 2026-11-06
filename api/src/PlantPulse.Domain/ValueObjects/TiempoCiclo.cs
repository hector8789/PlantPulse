using PlantPulse.Domain.Common;

namespace PlantPulse.Domain.ValueObjects;

/// <summary>Tiempo de ciclo ideal: segundos que tarda una pieza a velocidad nominal. Siempre &gt; 0.</summary>
public sealed record TiempoCiclo
{
    public static class Errores
    {
        public const string Invalido = "tiempo_ciclo.invalido";
    }

    /// <summary>Un ciclo de más de una hora por pieza indica un error de captura.</summary>
    public const decimal MaximoSegundos = 3600m;

    public decimal Segundos { get; }

    private TiempoCiclo(decimal segundos) => Segundos = segundos;

    public static TiempoCiclo DeSegundos(decimal segundos)
    {
        if (segundos <= 0m || segundos > MaximoSegundos)
            throw new DomainException(Errores.Invalido,
                $"El tiempo de ciclo debe ser mayor a 0 y menor o igual a {MaximoSegundos} s; se recibió {segundos}.");
        // 3 decimales (milisegundos) es la precisión que se guarda en la base de datos.
        return new TiempoCiclo(Math.Round(segundos, 3, MidpointRounding.AwayFromZero));
    }

    /// <summary>Tiempo ideal para producir <paramref name="piezas"/> piezas, en segundos.</summary>
    public decimal SegundosPara(int piezas) => Segundos * piezas;

    public override string ToString() => $"{Segundos:0.###} s/pza";
}
