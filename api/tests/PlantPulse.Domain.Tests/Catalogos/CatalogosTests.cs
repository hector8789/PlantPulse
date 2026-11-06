using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.ValueObjects;
using Xunit;

namespace PlantPulse.Domain.Tests.Catalogos;

public class PlantaTests
{
    [Fact]
    public void Crear_normaliza_codigo_y_valida_zona_horaria()
    {
        var p = Planta.Crear(" leo ", "Planta León", "America/Mexico_City");
        Assert.Equal("LEO", p.Codigo);
        Assert.True(p.Activa);
        Assert.NotEqual(Guid.Empty, p.Id);
    }

    [Fact]
    public void Zona_horaria_inexistente_lanza() =>
        Lanza.Codigo(Planta.Errores.ZonaHorariaInvalida, () => Planta.Crear("LEO", "León", "Mars/Olympus_Mons"));

    [Fact]
    public void Turno_con_numero_fuera_de_1_a_3_lanza()
    {
        var e = new Escenario();
        Lanza.Codigo(Planta.Errores.TurnoInvalido, () => e.Planta.AgregarTurno(4, "Cuarto", HorarioTurno.Crear(new(1, 0), new(3, 0))));
    }

    [Fact]
    public void Turno_duplicado_lanza()
    {
        var p = Planta.Crear("SIL", "Silao", "America/Mexico_City");
        p.AgregarTurno(1, "Matutino", HorarioTurno.Crear(new(6, 0), new(14, 0)));
        Lanza.Codigo(Planta.Errores.TurnoDuplicado, () => p.AgregarTurno(1, "Otro", HorarioTurno.Crear(new(14, 0), new(22, 0))));
    }

    [Fact]
    public void Turnos_traslapados_lanzan()
    {
        var p = Planta.Crear("SIL", "Silao", "America/Mexico_City");
        p.AgregarTurno(1, "Matutino", HorarioTurno.Crear(new(6, 0), new(14, 0)));
        Lanza.Codigo(Planta.Errores.TurnosTraslapados, () => p.AgregarTurno(2, "Vespertino", HorarioTurno.Crear(new(13, 0), new(21, 0))));
    }

    [Fact]
    public void FechaLocal_usa_la_zona_de_la_planta()
    {
        var e = new Escenario();
        // 03:00 UTC del martes = 21:00 del lunes en León.
        Assert.Equal(Escenario.Lunes, e.Planta.FechaLocal(Escenario.Utc(2026, 11, 3, 3, 0)));
        Assert.Equal(Escenario.Martes, e.Planta.FechaLocal(Escenario.Utc(2026, 11, 3, 6, 0)));
    }
}

public class OrdenProduccionTests
{
    [Fact]
    public void Toma_la_planta_de_la_linea_y_nace_liberada()
    {
        var e = new Escenario();
        Assert.Equal(e.Planta.Id, e.Orden.PlantaId);
        Assert.Equal(e.Linea.Id, e.Orden.LineaId);
        Assert.Equal(EstadoOrden.Liberada, e.Orden.Estado);
        Assert.Equal("CAR-4521", e.Orden.MaterialCodigo);
    }

    [Fact]
    public void Cantidad_cero_lanza()
    {
        var e = new Escenario();
        Lanza.Codigo(OrdenProduccion.Errores.DatoInvalido,
            () => OrdenProduccion.Crear("1", e.Linea, "M", "Mat", 0, Escenario.Lunes, Escenario.Lunes));
    }

    [Fact]
    public void Fechas_invertidas_lanzan()
    {
        var e = new Escenario();
        Lanza.Codigo(OrdenProduccion.Errores.DatoInvalido,
            () => OrdenProduccion.Crear("1", e.Linea, "M", "Mat", 10, Escenario.Martes, Escenario.Lunes));
    }

    [Fact]
    public void Tiempo_de_ciclo_de_la_orden_sobrescribe_el_estandar_de_la_linea()
    {
        var e = new Escenario();
        var propia = OrdenProduccion.Crear("2", e.Linea, "M", "Mat", 10, Escenario.Lunes, Escenario.Lunes, TiempoCiclo.DeSegundos(60m));
        Assert.Equal(45m, e.Orden.ResolverTiempoCiclo(e.Linea).Segundos);
        Assert.Equal(60m, propia.ResolverTiempoCiclo(e.Linea).Segundos);
    }

    [Fact]
    public void Resolver_ciclo_con_otra_linea_lanza()
    {
        var e = new Escenario();
        var otra = Linea.Crear(e.Planta, "L99", "Otra", TiempoCiclo.DeSegundos(10m));
        Lanza.Codigo(OrdenProduccion.Errores.LineaDistinta, () => e.Orden.ResolverTiempoCiclo(otra));
    }

    [Fact]
    public void Transiciones_de_estado()
    {
        var e = new Escenario();
        e.Orden.Iniciar();
        e.Orden.Iniciar(); // idempotente
        Assert.Equal(EstadoOrden.EnProceso, e.Orden.Estado);
        e.Orden.Terminar();
        Assert.False(e.Orden.AceptaProduccion);
        Lanza.Codigo(OrdenProduccion.Errores.TransicionInvalida, e.Orden.Iniciar);
        Lanza.Codigo(OrdenProduccion.Errores.TransicionInvalida, e.Orden.Terminar);
    }
}
