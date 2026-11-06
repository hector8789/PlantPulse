using PlantPulse.Domain.Common;

namespace PlantPulse.Domain.ValueObjects;

/// <summary>
/// Hora de inicio y fin de un turno en hora local de la planta.
/// Si Fin ≤ Inicio el turno cruza la medianoche (22:00–06:00) y termina el día siguiente.
/// </summary>
public sealed record HorarioTurno
{
    public static class Errores
    {
        public const string Invalido = "horario_turno.invalido";
    }

    public static readonly TimeSpan DuracionMinima = TimeSpan.FromHours(1);
    public static readonly TimeSpan DuracionMaxima = TimeSpan.FromHours(12);

    public TimeOnly Inicio { get; }
    public TimeOnly Fin { get; }

    private HorarioTurno(TimeOnly inicio, TimeOnly fin)
    {
        Inicio = inicio;
        Fin = fin;
    }

    public static HorarioTurno Crear(TimeOnly inicio, TimeOnly fin)
    {
        var horario = new HorarioTurno(inicio, fin);
        if (horario.Duracion < DuracionMinima || horario.Duracion > DuracionMaxima)
            throw new DomainException(Errores.Invalido,
                $"Un turno debe durar entre {DuracionMinima.TotalHours} y {DuracionMaxima.TotalHours} horas ({inicio:HH\\:mm}–{fin:HH\\:mm}).");
        return horario;
    }

    public bool CruzaMedianoche => Fin <= Inicio;

    public TimeSpan Duracion => CruzaMedianoche
        ? TimeSpan.FromDays(1) - Inicio.ToTimeSpan() + Fin.ToTimeSpan()
        : Fin.ToTimeSpan() - Inicio.ToTimeSpan();

    /// <summary>
    /// Inicio y fin en hora local para una fecha operativa. La fecha operativa es la del inicio:
    /// el turno nocturno del lunes termina el martes pero pertenece al lunes.
    /// </summary>
    public (DateTime InicioLocal, DateTime FinLocal) VentanaLocal(DateOnly fechaOperativa)
    {
        var inicio = fechaOperativa.ToDateTime(Inicio, DateTimeKind.Unspecified);
        return (inicio, inicio + Duracion);
    }

    /// <summary>¿Se traslapa con otro horario? Compara sobre un día circular de 24 h.</summary>
    public bool SeTraslapaCon(HorarioTurno otro)
    {
        var dia = TimeSpan.FromDays(1).Ticks;
        var (a0, a1) = (Inicio.Ticks, Inicio.Ticks + Duracion.Ticks);
        var (b0, b1) = (otro.Inicio.Ticks, otro.Inicio.Ticks + otro.Duracion.Ticks);
        // Se prueba el otro intervalo desplazado un día hacia atrás, sin desplazar y un día hacia adelante.
        for (var k = -1; k <= 1; k++)
        {
            var (c0, c1) = (b0 + k * dia, b1 + k * dia);
            if (a0 < c1 && c0 < a1) return true;
        }
        return false;
    }

    public override string ToString() => $"{Inicio:HH\\:mm}–{Fin:HH\\:mm}";
}
