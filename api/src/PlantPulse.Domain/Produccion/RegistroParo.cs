using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Common;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Domain.Produccion;

/// <summary>
/// Paro dentro de un turno de línea. Siempre cae completo dentro de la ventana del turno.
/// Copia el tipo de la causa al capturar: si el catálogo cambia después, el OEE histórico no se altera.
/// </summary>
public sealed class RegistroParo : Entity
{
    public Guid TurnoLineaId { get; private set; }
    public Guid CausaParoId { get; private set; }
    public TipoParo Tipo { get; private set; }
    public DateTimeOffset InicioUtc { get; private set; }
    public DateTimeOffset FinUtc { get; private set; }
    public string? Comentario { get; private set; }
    public DateTimeOffset RegistradoEnUtc { get; private set; }

    public Duracion Duracion => Duracion.Entre(InicioUtc, FinUtc);

    public bool EsPlanificado => Tipo == TipoParo.Planificado;

    private RegistroParo() { }

    internal RegistroParo(
        Guid id, Guid turnoLineaId, CausaParo causa, DateTimeOffset inicioUtc, DateTimeOffset finUtc,
        string? comentario, DateTimeOffset ahoraUtc)
        : base(id)
    {
        TurnoLineaId = turnoLineaId;
        CausaParoId = causa.Id;
        Tipo = causa.Tipo;
        InicioUtc = inicioUtc;
        FinUtc = finUtc;
        Comentario = comentario;
        RegistradoEnUtc = ahoraUtc;
    }

    internal bool SeTraslapaCon(DateTimeOffset inicioUtc, DateTimeOffset finUtc) =>
        InicioUtc < finUtc && inicioUtc < FinUtc;
}
