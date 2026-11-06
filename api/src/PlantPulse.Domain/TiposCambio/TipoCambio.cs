using PlantPulse.Domain.Common;

namespace PlantPulse.Domain.TiposCambio;

/// <summary>
/// Tipo de cambio publicado para una fecha (Banxico FIX, serie SF43718: pesos por dólar).
/// Se guarda en la base para no consultar Banxico en cada reporte y para que Power BI lo use.
/// Banxico solo publica en días hábiles: para un sábado se usa el último publicado.
/// </summary>
public sealed class TipoCambio : AggregateRoot
{
    public static class Errores
    {
        public const string Invalido = "tipo_cambio.invalido";
    }

    public const string SerieFix = "SF43718";

    public string Serie { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public decimal PesosPorDolar { get; private set; }
    public string Fuente { get; private set; } = null!;
    public DateTimeOffset ObtenidoEnUtc { get; private set; }

    private TipoCambio() { }

    private TipoCambio(Guid id) : base(id) { }

    public static TipoCambio Crear(string serie, DateOnly fecha, decimal pesosPorDolar, string fuente, DateTimeOffset obtenidoEnUtc)
    {
        if (pesosPorDolar <= 0m || pesosPorDolar > 1000m)
            throw new DomainException(Errores.Invalido, $"Tipo de cambio fuera de rango: {pesosPorDolar}.");

        return new TipoCambio(NuevoId())
        {
            Serie = Guard.Texto(serie, 20, Errores.Invalido, "La serie"),
            Fecha = fecha,
            PesosPorDolar = Math.Round(pesosPorDolar, 4, MidpointRounding.AwayFromZero),
            Fuente = Guard.Texto(fuente, 40, Errores.Invalido, "La fuente"),
            ObtenidoEnUtc = Guard.Utc(obtenidoEnUtc),
        };
    }

    /// <summary>
    /// Tipo vigente para una fecha: el publicado ese día o, si no hay (fin de semana o festivo), el último anterior.
    /// Si la fecha es anterior a todos, usa el más antiguo. Devuelve null si la lista está vacía.
    /// </summary>
    public static TipoCambio? VigentePara(DateOnly fecha, IReadOnlyList<TipoCambio> ordenadosPorFecha)
    {
        if (ordenadosPorFecha.Count == 0) return null;
        TipoCambio? vigente = null;
        foreach (var t in ordenadosPorFecha)
        {
            if (t.Fecha > fecha) break;
            vigente = t;
        }
        return vigente ?? ordenadosPorFecha[0];
    }
}
