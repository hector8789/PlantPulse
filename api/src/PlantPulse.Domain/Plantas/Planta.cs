using PlantPulse.Domain.Common;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Domain.Plantas;

/// <summary>
/// Planta de manufactura. Es la frontera de seguridad: el RLS de Power BI y el acceso por planta
/// del API filtran por <see cref="Entity.Id"/>. Contiene la definición de sus turnos.
/// </summary>
public sealed class Planta : AggregateRoot
{
    public static class Errores
    {
        public const string DatoInvalido = "planta.dato_invalido";
        public const string ZonaHorariaInvalida = "planta.zona_horaria_invalida";
        public const string TurnoInvalido = "planta.turno_invalido";
        public const string TurnoDuplicado = "planta.turno_duplicado";
        public const string TurnosTraslapados = "planta.turnos_traslapados";
        public const string TurnoNoExiste = "planta.turno_no_existe";
    }

    /// <summary>Recomendación aceptada: hasta 3 turnos por planta, con horario configurable.</summary>
    public const int MaximoTurnos = 3;

    private readonly List<Turno> _turnos = [];

    public string Codigo { get; private set; } = null!;
    public string Nombre { get; private set; } = null!;

    /// <summary>Id IANA, p. ej. "America/Mexico_City". Define la fecha operativa local.</summary>
    public string ZonaHoraria { get; private set; } = null!;

    public bool Activa { get; private set; }

    public IReadOnlyCollection<Turno> Turnos => _turnos.AsReadOnly();

    private Planta() { }

    private Planta(Guid id, string codigo, string nombre, string zonaHoraria) : base(id)
    {
        Codigo = codigo;
        Nombre = nombre;
        ZonaHoraria = zonaHoraria;
        Activa = true;
    }

    public static Planta Crear(string codigo, string nombre, string zonaHoraria)
    {
        var zona = Guard.Texto(zonaHoraria, 64, Errores.ZonaHorariaInvalida, "La zona horaria");
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zona, out _))
            throw new DomainException(Errores.ZonaHorariaInvalida, $"La zona horaria '{zona}' no existe.");

        return new Planta(
            NuevoId(),
            Guard.Texto(codigo, 20, Errores.DatoInvalido, "El código de planta").ToUpperInvariant(),
            Guard.Texto(nombre, 120, Errores.DatoInvalido, "El nombre de planta"),
            zona);
    }

    public Turno AgregarTurno(int numero, string nombre, HorarioTurno horario)
    {
        if (numero is < 1 or > MaximoTurnos)
            throw new DomainException(Errores.TurnoInvalido, $"El número de turno debe estar entre 1 y {MaximoTurnos}.");
        if (_turnos.Any(t => t.Numero == numero))
            throw new DomainException(Errores.TurnoDuplicado, $"La planta {Codigo} ya tiene el turno {numero}.");

        var traslape = _turnos.FirstOrDefault(t => t.Horario.SeTraslapaCon(horario));
        if (traslape is not null)
            throw new DomainException(Errores.TurnosTraslapados,
                $"El horario {horario} se traslapa con el turno {traslape.Numero} ({traslape.Horario}).");

        var turno = new Turno(NuevoId(), Id, numero, nombre, horario);
        _turnos.Add(turno);
        return turno;
    }

    public Turno ObtenerTurno(Guid turnoId) =>
        _turnos.FirstOrDefault(t => t.Id == turnoId)
        ?? throw new DomainException(Errores.TurnoNoExiste, $"El turno {turnoId} no pertenece a la planta {Codigo}.");

    public TimeZoneInfo ObtenerZonaHoraria() => TimeZoneInfo.FindSystemTimeZoneById(ZonaHoraria);

    /// <summary>Fecha calendario local de la planta en el instante dado.</summary>
    public DateOnly FechaLocal(DateTimeOffset instanteUtc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instanteUtc, ObtenerZonaHoraria()).DateTime);

    /// <summary>Convierte una hora local de la planta (Kind Unspecified) a UTC.</summary>
    public DateTimeOffset LocalAUtc(DateTime local)
    {
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), ObtenerZonaHoraria());
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    public void Desactivar() => Activa = false;
}
