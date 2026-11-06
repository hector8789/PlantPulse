using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Common;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.Produccion;
using PlantPulse.Domain.ValueObjects;
using Xunit;

namespace PlantPulse.Domain.Tests;

/// <summary>
/// Datos comunes. Planta León en America/Mexico_City (UTC−6, sin horario de verano desde 2022).
/// Lunes 2026-11-02: Matutino 06–14 local = 12:00–20:00 UTC; Nocturno 22–06 local = 04:00–12:00 UTC del martes.
/// </summary>
internal sealed class Escenario
{
    public static readonly DateOnly Lunes = new(2026, 11, 2);
    public static readonly DateOnly Martes = new(2026, 11, 3);

    public Planta Planta { get; }
    public Turno Matutino { get; }
    public Turno Vespertino { get; }
    public Turno Nocturno { get; }
    public Linea Linea { get; }
    public OrdenProduccion Orden { get; }
    public CausaParo Comida { get; }
    public CausaParo FallaMecanica { get; }
    public CausaParo FaltaMaterial { get; }

    /// <summary>Inicio y fin UTC del Matutino del lunes.</summary>
    public static readonly DateTimeOffset InicioMatutino = Utc(2026, 11, 2, 12, 0);
    public static readonly DateTimeOffset FinMatutino = Utc(2026, 11, 2, 20, 0);

    /// <summary>Un momento después del fin del Matutino: todo es capturable y cerrable.</summary>
    public static readonly DateTimeOffset DespuesDelMatutino = FinMatutino.AddMinutes(10);

    public Escenario()
    {
        Planta = Planta.Crear("leo", "Planta León", "America/Mexico_City");
        Matutino = Planta.AgregarTurno(1, "Matutino", HorarioTurno.Crear(new(6, 0), new(14, 0)));
        Vespertino = Planta.AgregarTurno(2, "Vespertino", HorarioTurno.Crear(new(14, 0), new(22, 0)));
        Nocturno = Planta.AgregarTurno(3, "Nocturno", HorarioTurno.Crear(new(22, 0), new(6, 0)));

        Linea = Linea.Crear(Planta, "leo-l01", "Fundición a presión 1", TiempoCiclo.DeSegundos(45m));
        Orden = OrdenProduccion.Crear("1000001", Linea, "car-4521", "Carcasa de transmisión", 1500, Lunes, Lunes.AddDays(2));

        Comida = CausaParo.Crear("pln-com", "Comida", TipoParo.Planificado, CategoriaParo.Programado);
        FallaMecanica = CausaParo.Crear("np-mec", "Falla mecánica", TipoParo.NoPlanificado, CategoriaParo.Mecanica);
        FaltaMaterial = CausaParo.Crear("np-mat", "Falta de material", TipoParo.NoPlanificado, CategoriaParo.Material);
    }

    public TurnoLinea AbrirMatutino(DateTimeOffset? ahoraUtc = null) =>
        TurnoLinea.Abrir(Planta, Linea, Matutino.Id, Lunes, ahoraUtc ?? DespuesDelMatutino);

    public static DateTimeOffset Utc(int anio, int mes, int dia, int hora, int minuto) =>
        new(anio, mes, dia, hora, minuto, 0, TimeSpan.Zero);
}

internal static class Lanza
{
    /// <summary>Verifica que la acción lance <see cref="DomainException"/> con el código esperado.</summary>
    public static void Codigo(string codigoEsperado, Action accion)
    {
        var ex = Assert.Throws<DomainException>(accion);
        Assert.Equal(codigoEsperado, ex.Codigo);
    }
}
