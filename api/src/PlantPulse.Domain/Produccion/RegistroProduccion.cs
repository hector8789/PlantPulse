using PlantPulse.Domain.Common;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Domain.Produccion;

/// <summary>
/// Piezas producidas de una orden dentro de un turno de línea. Hay a lo más uno por orden en cada turno.
/// Guarda una copia del tiempo de ciclo ideal vigente al capturar, para que cambiar el estándar
/// de la línea después no altere el OEE histórico.
/// </summary>
public sealed class RegistroProduccion : Entity
{
    public Guid TurnoLineaId { get; private set; }
    public Guid OrdenProduccionId { get; private set; }
    public TiempoCiclo TiempoCicloIdeal { get; private set; } = null!;
    public int PiezasTotales { get; private set; }
    public int PiezasBuenas { get; private set; }
    public DateTimeOffset RegistradoEnUtc { get; private set; }
    public DateTimeOffset? ModificadoEnUtc { get; private set; }

    public ConteoPiezas Conteo => ConteoPiezas.Crear(PiezasTotales, PiezasBuenas);

    public int Scrap => PiezasTotales - PiezasBuenas;

    public decimal SegundosIdeales => TiempoCicloIdeal.SegundosPara(PiezasTotales);

    private RegistroProduccion() { }

    internal RegistroProduccion(
        Guid id, Guid turnoLineaId, Guid ordenProduccionId, TiempoCiclo tiempoCiclo, ConteoPiezas conteo, DateTimeOffset ahoraUtc)
        : base(id)
    {
        TurnoLineaId = turnoLineaId;
        OrdenProduccionId = ordenProduccionId;
        TiempoCicloIdeal = tiempoCiclo;
        PiezasTotales = conteo.Totales;
        PiezasBuenas = conteo.Buenas;
        RegistradoEnUtc = ahoraUtc;
    }

    internal void Corregir(ConteoPiezas conteo, DateTimeOffset ahoraUtc)
    {
        PiezasTotales = conteo.Totales;
        PiezasBuenas = conteo.Buenas;
        ModificadoEnUtc = ahoraUtc;
    }
}
