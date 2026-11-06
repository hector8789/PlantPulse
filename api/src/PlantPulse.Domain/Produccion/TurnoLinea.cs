using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Common;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Domain.Produccion;

public enum EstadoTurno
{
    Abierto = 1,
    Cerrado = 2,
}

/// <summary>
/// Agregado principal: un turno de una línea en una fecha operativa.
/// Contiene los registros de producción (uno por orden) y los paros del turno; de ellos sale el OEE.
/// <para>Reglas acordadas:</para>
/// <list type="bullet">
/// <item>La fecha operativa es la del inicio del turno (el nocturno del lunes cuenta como lunes).</item>
/// <item>Se captura hasta <see cref="DiasMaximosCapturaRetroactiva"/> días atrás y nunca a futuro.</item>
/// <item>Un paro no puede salir de la ventana del turno; si cruza, se capturan dos paros.</item>
/// <item>Al cerrar, el turno queda inmutable; solo un Admin lo reabre (el rol se valida en Application).</item>
/// </list>
/// </summary>
public sealed class TurnoLinea : AggregateRoot
{
    public static class Errores
    {
        public const string LineaDePlantaDistinta = "turno_linea.linea_de_planta_distinta";
        public const string LineaInactiva = "turno_linea.linea_inactiva";
        public const string FechaFutura = "turno_linea.fecha_futura";
        public const string FueraDeVentanaCaptura = "turno_linea.fuera_de_ventana_captura";
        public const string TurnoNoIniciado = "turno_linea.turno_no_iniciado";
        public const string TurnoCerrado = "turno_linea.cerrado";
        public const string TurnoNoCerrado = "turno_linea.no_cerrado";
        public const string TurnoNoTerminado = "turno_linea.turno_no_terminado";
        public const string OrdenDeOtraLinea = "turno_linea.orden_de_otra_linea";
        public const string OrdenNoAceptaProduccion = "turno_linea.orden_no_acepta_produccion";
        public const string OrdenYaRegistrada = "turno_linea.orden_ya_registrada";
        public const string RegistroNoExiste = "turno_linea.registro_no_existe";
        public const string CausaInactiva = "turno_linea.causa_inactiva";
        public const string ParoInvalido = "turno_linea.paro_invalido";
        public const string ParoFueraDeTurno = "turno_linea.paro_fuera_de_turno";
        public const string ParoFuturo = "turno_linea.paro_futuro";
        public const string ParoTraslapado = "turno_linea.paro_traslapado";
        public const string ParoNoExiste = "turno_linea.paro_no_existe";
    }

    public const int DiasMaximosCapturaRetroactiva = 7;
    public const int MaximoComentario = 500;

    private readonly List<RegistroProduccion> _registros = [];
    private readonly List<RegistroParo> _paros = [];

    /// <summary>Copia de la planta de la línea; permite filtrar por planta (RLS) sin joins.</summary>
    public Guid PlantaId { get; private set; }
    public Guid LineaId { get; private set; }
    public Guid TurnoId { get; private set; }
    public DateOnly FechaOperativa { get; private set; }
    public DateTimeOffset InicioUtc { get; private set; }
    public DateTimeOffset FinUtc { get; private set; }
    public EstadoTurno Estado { get; private set; }
    public DateTimeOffset AbiertoEnUtc { get; private set; }
    public DateTimeOffset? CerradoEnUtc { get; private set; }

    public IReadOnlyCollection<RegistroProduccion> Registros => _registros.AsReadOnly();
    public IReadOnlyCollection<RegistroParo> Paros => _paros.AsReadOnly();

    public bool EstaCerrado => Estado == EstadoTurno.Cerrado;

    public Duracion DuracionTurno => Duracion.Entre(InicioUtc, FinUtc);

    private TurnoLinea() { }

    private TurnoLinea(Guid id) : base(id) { }

    /// <summary>
    /// Abre el turno de una línea. La unicidad (línea + fecha + turno) la garantiza un índice único
    /// y Application responde 409 si ya existe.
    /// </summary>
    public static TurnoLinea Abrir(Planta planta, Linea linea, Guid turnoId, DateOnly fechaOperativa, DateTimeOffset ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(planta);
        ArgumentNullException.ThrowIfNull(linea);
        ahoraUtc = Guard.Utc(ahoraUtc);

        if (linea.PlantaId != planta.Id)
            throw new DomainException(Errores.LineaDePlantaDistinta, $"La línea {linea.Codigo} no pertenece a la planta {planta.Codigo}.");
        if (!linea.Activa)
            throw new DomainException(Errores.LineaInactiva, $"La línea {linea.Codigo} está inactiva.");

        var turno = planta.ObtenerTurno(turnoId);

        var hoy = planta.FechaLocal(ahoraUtc);
        if (fechaOperativa > hoy)
            throw new DomainException(Errores.FechaFutura, $"No se puede capturar la fecha {fechaOperativa:yyyy-MM-dd}: es futura.");
        if (fechaOperativa < hoy.AddDays(-DiasMaximosCapturaRetroactiva))
            throw new DomainException(Errores.FueraDeVentanaCaptura,
                $"Solo se puede capturar hasta {DiasMaximosCapturaRetroactiva} días atrás ({hoy.AddDays(-DiasMaximosCapturaRetroactiva):yyyy-MM-dd}).");

        var (inicioLocal, finLocal) = turno.Horario.VentanaLocal(fechaOperativa);
        var inicioUtc = planta.LocalAUtc(inicioLocal);
        var finUtc = planta.LocalAUtc(finLocal);

        if (ahoraUtc < inicioUtc)
            throw new DomainException(Errores.TurnoNoIniciado,
                $"El turno {turno.Numero} del {fechaOperativa:yyyy-MM-dd} todavía no inicia.");

        return new TurnoLinea(NuevoId())
        {
            PlantaId = planta.Id,
            LineaId = linea.Id,
            TurnoId = turno.Id,
            FechaOperativa = fechaOperativa,
            InicioUtc = inicioUtc,
            FinUtc = finUtc,
            Estado = EstadoTurno.Abierto,
            AbiertoEnUtc = ahoraUtc,
        };
    }

    // ───────────── Producción ─────────────

    public RegistroProduccion RegistrarProduccion(
        OrdenProduccion orden, Linea linea, int piezasTotales, int piezasBuenas, DateTimeOffset ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(orden);
        ArgumentNullException.ThrowIfNull(linea);
        AsegurarAbierto();

        if (orden.LineaId != LineaId || linea.Id != LineaId)
            throw new DomainException(Errores.OrdenDeOtraLinea, $"La orden {orden.Numero} no pertenece a esta línea.");
        if (!orden.AceptaProduccion)
            throw new DomainException(Errores.OrdenNoAceptaProduccion, $"La orden {orden.Numero} está {orden.Estado} y no acepta producción.");
        if (_registros.Any(r => r.OrdenProduccionId == orden.Id))
            throw new DomainException(Errores.OrdenYaRegistrada,
                $"La orden {orden.Numero} ya tiene producción en este turno; corrige el registro existente.");

        var registro = new RegistroProduccion(
            NuevoId(), Id, orden.Id, orden.ResolverTiempoCiclo(linea),
            ConteoPiezas.Crear(piezasTotales, piezasBuenas), Guard.Utc(ahoraUtc));
        _registros.Add(registro);
        return registro;
    }

    public void CorregirProduccion(Guid registroId, int piezasTotales, int piezasBuenas, DateTimeOffset ahoraUtc)
    {
        AsegurarAbierto();
        var registro = _registros.FirstOrDefault(r => r.Id == registroId)
            ?? throw new DomainException(Errores.RegistroNoExiste, $"El registro {registroId} no existe en este turno.");
        registro.Corregir(ConteoPiezas.Crear(piezasTotales, piezasBuenas), Guard.Utc(ahoraUtc));
    }

    // ───────────── Paros ─────────────

    public RegistroParo RegistrarParo(
        CausaParo causa, DateTimeOffset inicioUtc, DateTimeOffset finUtc, string? comentario, DateTimeOffset ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(causa);
        AsegurarAbierto();
        inicioUtc = Guard.Utc(inicioUtc);
        finUtc = Guard.Utc(finUtc);
        ahoraUtc = Guard.Utc(ahoraUtc);

        if (!causa.Activa)
            throw new DomainException(Errores.CausaInactiva, $"La causa {causa.Codigo} está inactiva.");
        if (finUtc <= inicioUtc)
            throw new DomainException(Errores.ParoInvalido, "El fin del paro debe ser posterior a su inicio.");
        if (inicioUtc < InicioUtc || finUtc > FinUtc)
            throw new DomainException(Errores.ParoFueraDeTurno,
                "El paro sale de la ventana del turno. Si cruza al siguiente turno, captúralo como dos paros.");
        if (finUtc > ahoraUtc)
            throw new DomainException(Errores.ParoFuturo, "No se puede capturar un paro que termina en el futuro.");

        var traslapado = _paros.FirstOrDefault(p => p.SeTraslapaCon(inicioUtc, finUtc));
        if (traslapado is not null)
            throw new DomainException(Errores.ParoTraslapado,
                $"El paro se traslapa con otro ya capturado ({traslapado.InicioUtc:HH\\:mm}–{traslapado.FinUtc:HH\\:mm} UTC).");

        var paro = new RegistroParo(
            NuevoId(), Id, causa, inicioUtc, finUtc,
            Guard.TextoOpcional(comentario, MaximoComentario, Errores.ParoInvalido, "El comentario"),
            ahoraUtc);
        _paros.Add(paro);
        return paro;
    }

    public void EliminarParo(Guid paroId)
    {
        AsegurarAbierto();
        var paro = _paros.FirstOrDefault(p => p.Id == paroId)
            ?? throw new DomainException(Errores.ParoNoExiste, $"El paro {paroId} no existe en este turno.");
        _paros.Remove(paro);
    }

    // ───────────── Ciclo de vida ─────────────

    /// <summary>Cierra el turno. Solo después de que termina; a partir de aquí es inmutable.</summary>
    public void Cerrar(DateTimeOffset ahoraUtc)
    {
        AsegurarAbierto();
        ahoraUtc = Guard.Utc(ahoraUtc);
        if (ahoraUtc < FinUtc)
            throw new DomainException(Errores.TurnoNoTerminado, "El turno no se puede cerrar antes de que termine.");

        Estado = EstadoTurno.Cerrado;
        CerradoEnUtc = ahoraUtc;
        Raise(new TurnoLineaCerrado(Id, PlantaId, LineaId, FechaOperativa, ahoraUtc));
    }

    /// <summary>Reabre un turno cerrado. Que solo lo haga un Admin se valida en Application.</summary>
    public void Reabrir(DateTimeOffset ahoraUtc)
    {
        if (!EstaCerrado)
            throw new DomainException(Errores.TurnoNoCerrado, "El turno no está cerrado.");
        ahoraUtc = Guard.Utc(ahoraUtc);
        Estado = EstadoTurno.Abierto;
        CerradoEnUtc = null;
        Raise(new TurnoLineaReabierto(Id, PlantaId, LineaId, FechaOperativa, ahoraUtc));
    }

    // ───────────── KPI ─────────────

    public ResultadoOee CalcularOee()
    {
        var planificados = Duracion.Suma(_paros.Where(p => p.EsPlanificado).Select(p => p.Duracion));
        var noPlanificados = Duracion.Suma(_paros.Where(p => !p.EsPlanificado).Select(p => p.Duracion));
        var segundosIdeales = _registros.Sum(r => r.SegundosIdeales);
        var piezas = _registros.Aggregate(ConteoPiezas.Vacio, (acc, r) => acc + r.Conteo);

        return ResultadoOee.Calcular(DuracionTurno, planificados, noPlanificados, segundosIdeales, piezas);
    }

    private void AsegurarAbierto()
    {
        if (EstaCerrado)
            throw new DomainException(Errores.TurnoCerrado, "El turno está cerrado y no admite cambios.");
    }
}
