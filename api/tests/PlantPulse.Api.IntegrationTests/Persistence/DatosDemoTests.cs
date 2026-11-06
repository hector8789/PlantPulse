using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Produccion;
using PlantPulse.Infrastructure.Persistence.Seed;
using Xunit;

namespace PlantPulse.Api.IntegrationTests.Persistence;

/// <summary>Valida el generador del seeder sin base de datos.</summary>
public class DatosDemoTests
{
    // Miércoles 4 de noviembre de 2026, 15:40 en León.
    private static readonly DateTimeOffset Ahora = new(2026, 11, 4, 21, 40, 0, TimeSpan.Zero);
    private static readonly DatosDemo Datos = DatosDemo.Generar(Ahora);

    [Fact]
    public void Genera_catalogos_esperados()
    {
        Assert.Equal(2, Datos.Plantas.Count);
        Assert.All(Datos.Plantas, p => Assert.Equal(3, p.Turnos.Count));
        Assert.Equal(5, Datos.Lineas.Count);
        Assert.Equal(10, Datos.Causas.Count);
        Assert.Equal(3, Datos.Causas.Count(c => c.Tipo == TipoParo.Planificado));
    }

    [Fact]
    public void Genera_semanas_de_historia_con_todos_los_turnos_cerrados()
    {
        Assert.True(Datos.Turnos.Count > 250, $"Solo se generaron {Datos.Turnos.Count} turnos.");
        Assert.All(Datos.Turnos, t =>
        {
            Assert.Equal(EstadoTurno.Cerrado, t.Estado);
            Assert.True(t.FinUtc <= Ahora);
            Assert.NotEqual(DayOfWeek.Sunday, t.FechaOperativa.DayOfWeek);
            Assert.Empty(t.DomainEvents);
        });
    }

    [Fact]
    public void Turnos_unicos_por_linea_fecha_y_turno()
    {
        var claves = Datos.Turnos.Select(t => (t.LineaId, t.FechaOperativa, t.TurnoId)).ToList();
        Assert.Equal(claves.Count, claves.Distinct().Count());
    }

    [Fact]
    public void Oee_dentro_de_rangos_realistas()
    {
        var resultados = Datos.Turnos.Select(t => t.CalcularOee()).ToList();
        Assert.All(resultados, r =>
        {
            Assert.InRange(r.Oee.Valor, 0.20m, 0.95m);
            Assert.False(r.DesempenoExcedido);
        });
        var promedio = resultados.Average(r => r.Oee.Valor);
        Assert.InRange(promedio, 0.60m, 0.85m);
    }

    [Fact]
    public void Pareto_lo_encabeza_falla_mecanica()
    {
        var porCausa = Datos.Turnos.SelectMany(t => t.Paros)
            .Where(p => !p.EsPlanificado)
            .GroupBy(p => p.CausaParoId)
            .OrderByDescending(g => g.Sum(p => p.Duracion.TotalMinutos))
            .First().Key;
        Assert.Equal("NP-MEC", Datos.Causas.Single(c => c.Id == porCausa).Codigo);
    }

    [Fact]
    public void Hay_turnos_con_cambio_de_orden()
    {
        Assert.Contains(true, Datos.Turnos.Select(t => t.Registros.Count == 2));
    }

    [Fact]
    public void Estados_de_orden_coherentes_con_la_fecha()
    {
        Assert.Contains(EstadoOrden.Terminada, Datos.Ordenes.Select(o => o.Estado));
        Assert.Contains(EstadoOrden.EnProceso, Datos.Ordenes.Select(o => o.Estado));
        Assert.Contains(EstadoOrden.Liberada, Datos.Ordenes.Select(o => o.Estado));
        Assert.Equal(Datos.Ordenes.Count, Datos.Ordenes.Select(o => o.Numero).Distinct().Count());
        Assert.All(Datos.Ordenes, o => Assert.NotNull(o.CostoUnitarioMxn));
    }

    [Fact]
    public void Usuarios_demo_cubren_los_tres_roles()
    {
        Assert.Equal(4, DatosDemo.UsuariosDemo.Count);
        Assert.Equal(3, DatosDemo.UsuariosDemo.Select(u => u.Rol).Distinct().Count());
        Assert.Equal(412.50m, DatosDemo.CostoEstandar("car-4521"));
        Assert.Null(DatosDemo.CostoEstandar("NO-EXISTE"));
    }

    [Fact]
    public void Es_determinista()
    {
        var otra = DatosDemo.Generar(Ahora);
        Assert.Equal(
            Datos.Turnos.Sum(t => t.CalcularOee().Piezas.Totales),
            otra.Turnos.Sum(t => t.CalcularOee().Piezas.Totales));
        Assert.Equal(Datos.Turnos.Sum(t => t.Paros.Count), otra.Turnos.Sum(t => t.Paros.Count));
    }
}
