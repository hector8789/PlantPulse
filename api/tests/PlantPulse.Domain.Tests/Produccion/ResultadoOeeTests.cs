using PlantPulse.Domain.Produccion;
using PlantPulse.Domain.ValueObjects;
using Xunit;

namespace PlantPulse.Domain.Tests.Produccion;

public class ResultadoOeeTests
{
    private static readonly Duracion Turno8h = Duracion.DeMinutos(480);

    [Fact]
    public void Ejemplo_completo()
    {
        // 480 − 30 planificado = 450; 450 − 45 no planificado = 405 min = 24 300 s.
        // Ideal: 45 s × 486 = 21 870 s → desempeño 0.9. Calidad 474/486. OEE = 0.9 × 0.9 × 474/486 = 0.79.
        var r = ResultadoOee.Calcular(Turno8h, Duracion.DeMinutos(30), Duracion.DeMinutos(45), 45m * 486, ConteoPiezas.Crear(486, 474));

        Assert.Equal(450m, r.TiempoPlanificado.TotalMinutos);
        Assert.Equal(405m, r.TiempoOperativo.TotalMinutos);
        Assert.Equal(0.9m, r.Disponibilidad.Valor);
        Assert.Equal(0.9m, r.Desempeno.Valor);
        Assert.Equal(474m / 486m, r.Calidad.Valor);
        Assert.Equal(0.79m, Math.Round(r.Oee.Valor, 10));
        Assert.Equal(12, r.Scrap);
        Assert.False(r.DesempenoExcedido);
    }

    [Fact]
    public void Todo_el_turno_en_paro_planificado_da_cero_sin_dividir_entre_cero()
    {
        var r = ResultadoOee.Calcular(Turno8h, Turno8h, Duracion.Cero, 0m, ConteoPiezas.Vacio);
        Assert.Equal(Duracion.Cero, r.TiempoPlanificado);
        Assert.Equal(Porcentaje.Cero, r.Disponibilidad);
        Assert.Equal(Porcentaje.Cero, r.Desempeno);
        Assert.Equal(Porcentaje.Cero, r.Calidad);
        Assert.Equal(Porcentaje.Cero, r.Oee);
        Assert.False(r.DesempenoExcedido);
    }

    [Fact]
    public void Sin_tiempo_operativo_pero_con_piezas_marca_excedido()
    {
        var r = ResultadoOee.Calcular(Turno8h, Duracion.Cero, Turno8h, 100m, ConteoPiezas.Crear(10, 10));
        Assert.Equal(Porcentaje.Cero, r.Desempeno);
        Assert.True(r.DesempenoExcedido);
        Assert.Equal(Porcentaje.Cero, r.Oee);
    }

    [Fact]
    public void Desempeno_mayor_a_100_se_limita_y_se_marca()
    {
        // 480 min operativos = 28 800 s; ideal 30 000 s → 104.17%.
        var r = ResultadoOee.Calcular(Turno8h, Duracion.Cero, Duracion.Cero, 30_000m, ConteoPiezas.Crear(600, 600));
        Assert.Equal(Porcentaje.Cien, r.Desempeno);
        Assert.True(r.DesempenoExcedido);
        Assert.True(r.DesempenoCalculado > 1m);
        Assert.Equal(Porcentaje.Cien, r.Oee);
    }

    [Fact]
    public void Paros_planificados_mayores_al_turno_lanzan() =>
        Lanza.Codigo(ResultadoOee.Errores.TiemposInconsistentes,
            () => ResultadoOee.Calcular(Turno8h, Duracion.DeMinutos(481), Duracion.Cero, 0m, ConteoPiezas.Vacio));

    [Fact]
    public void Paros_no_planificados_mayores_al_tiempo_planificado_lanzan() =>
        Lanza.Codigo(ResultadoOee.Errores.TiemposInconsistentes,
            () => ResultadoOee.Calcular(Turno8h, Duracion.DeMinutos(30), Duracion.DeMinutos(451), 0m, ConteoPiezas.Vacio));
}
