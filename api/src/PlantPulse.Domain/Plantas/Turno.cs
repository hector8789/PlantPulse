using PlantPulse.Domain.Common;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Domain.Plantas;

/// <summary>Definición de un turno de la planta (Matutino 06:00–14:00). Pertenece al agregado <see cref="Planta"/>.</summary>
public sealed class Turno : Entity
{
    public Guid PlantaId { get; private set; }
    public int Numero { get; private set; }
    public string Nombre { get; private set; } = null!;
    public TimeOnly HoraInicio { get; private set; }
    public TimeOnly HoraFin { get; private set; }

    public HorarioTurno Horario => HorarioTurno.Crear(HoraInicio, HoraFin);

    private Turno() { }

    internal Turno(Guid id, Guid plantaId, int numero, string nombre, HorarioTurno horario) : base(id)
    {
        PlantaId = plantaId;
        Numero = numero;
        Nombre = Guard.Texto(nombre, 40, Planta.Errores.TurnoInvalido, "El nombre del turno");
        HoraInicio = horario.Inicio;
        HoraFin = horario.Fin;
    }
}
