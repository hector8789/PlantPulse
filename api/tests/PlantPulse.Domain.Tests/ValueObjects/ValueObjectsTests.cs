using PlantPulse.Domain.ValueObjects;
using Xunit;

namespace PlantPulse.Domain.Tests.ValueObjects;

public class PorcentajeTests
{
    [Theory]
    [InlineData("-0.0001")]
    [InlineData("1.0001")]
    public void Crear_fuera_de_0_a_1_lanza(string valor) =>
        Lanza.Codigo(Porcentaje.Errores.FueraDeRango, () => Porcentaje.Crear(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Crear_en_los_limites_es_valido()
    {
        Assert.Equal(0m, Porcentaje.Crear(0m).Valor);
        Assert.Equal(1m, Porcentaje.Crear(1m).Valor);
    }

    [Fact]
    public void DeRazon_con_denominador_cero_devuelve_el_valor_definido()
    {
        Assert.Equal(Porcentaje.Cero, Porcentaje.DeRazon(5m, 0m, Porcentaje.Cero));
        Assert.Equal(Porcentaje.Cien, Porcentaje.DeRazon(0m, 0m, Porcentaje.Cien));
    }

    [Fact]
    public void DeRazon_mayor_a_uno_lanza() =>
        Lanza.Codigo(Porcentaje.Errores.FueraDeRango, () => Porcentaje.DeRazon(11m, 10m, Porcentaje.Cero));

    [Fact]
    public void DeRazon_negativa_lanza() =>
        Lanza.Codigo(Porcentaje.Errores.Negativo, () => Porcentaje.DeRazon(-1m, 10m, Porcentaje.Cero));

    [Fact]
    public void DeRazonLimitada_limita_a_cien_y_conserva_el_valor_calculado()
    {
        var p = Porcentaje.DeRazonLimitada(12m, 10m, Porcentaje.Cero, out var calculado);
        Assert.Equal(Porcentaje.Cien, p);
        Assert.Equal(1.2m, calculado);
    }

    [Fact]
    public void Por_multiplica_y_EnEscala100_redondea()
    {
        var oee = Porcentaje.Crear(0.9m).Por(Porcentaje.Crear(0.9m)).Por(Porcentaje.Crear(0.95m));
        Assert.Equal(0.7695m, oee.Valor);
        Assert.Equal(76.95m, oee.EnEscala100());
        Assert.Equal(77.0m, oee.EnEscala100(0));
    }
}

public class DuracionTests
{
    [Fact]
    public void De_negativa_lanza() =>
        Lanza.Codigo(Duracion.Errores.Negativa, () => Duracion.De(TimeSpan.FromSeconds(-1)));

    [Fact]
    public void Resta_que_queda_negativa_lanza() =>
        Lanza.Codigo(Duracion.Errores.Negativa, () => _ = Duracion.DeMinutos(10) - Duracion.DeMinutos(11));

    [Fact]
    public void Suma_y_segundos_exactos()
    {
        var total = Duracion.Suma([Duracion.DeMinutos(30), Duracion.DeMinutos(15), Duracion.De(TimeSpan.FromMilliseconds(500))]);
        Assert.Equal(2700.5m, total.TotalSegundos);
        Assert.Equal(2700.5m / 60m, total.TotalMinutos);
    }
}

public class TiempoCicloTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]
    public void Fuera_de_rango_lanza(int segundos) =>
        Lanza.Codigo(TiempoCiclo.Errores.Invalido, () => TiempoCiclo.DeSegundos(segundos));

    [Fact]
    public void Redondea_a_milisegundos_y_calcula_tiempo_ideal()
    {
        var ciclo = TiempoCiclo.DeSegundos(45.12345m);
        Assert.Equal(45.123m, ciclo.Segundos);
        Assert.Equal(451.23m, ciclo.SegundosPara(10));
    }
}

public class ConteoPiezasTests
{
    [Fact]
    public void Buenas_mayores_que_totales_lanza() =>
        Lanza.Codigo(ConteoPiezas.Errores.Invalido, () => ConteoPiezas.Crear(10, 11));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(10, -1)]
    public void Negativos_lanzan(int totales, int buenas) =>
        Lanza.Codigo(ConteoPiezas.Errores.Invalido, () => ConteoPiezas.Crear(totales, buenas));

    [Fact]
    public void Scrap_calidad_y_tasa()
    {
        var c = ConteoPiezas.Crear(200, 190);
        Assert.Equal(10, c.Scrap);
        Assert.Equal(0.95m, c.Calidad.Valor);
        Assert.Equal(0.05m, c.TasaScrap.Valor);
    }

    [Fact]
    public void Sin_piezas_calidad_y_scrap_son_cero_sin_dividir_entre_cero()
    {
        Assert.Equal(Porcentaje.Cero, ConteoPiezas.Vacio.Calidad);
        Assert.Equal(Porcentaje.Cero, ConteoPiezas.Vacio.TasaScrap);
    }

    [Fact]
    public void Suma_acumula()
    {
        var c = ConteoPiezas.Crear(100, 98) + ConteoPiezas.Crear(50, 45);
        Assert.Equal(150, c.Totales);
        Assert.Equal(143, c.Buenas);
    }
}

public class HorarioTurnoTests
{
    [Fact]
    public void Turno_normal()
    {
        var h = HorarioTurno.Crear(new(6, 0), new(14, 0));
        Assert.False(h.CruzaMedianoche);
        Assert.Equal(TimeSpan.FromHours(8), h.Duracion);
    }

    [Fact]
    public void Turno_nocturno_cruza_medianoche_y_su_ventana_termina_al_dia_siguiente()
    {
        var h = HorarioTurno.Crear(new(22, 0), new(6, 0));
        Assert.True(h.CruzaMedianoche);
        Assert.Equal(TimeSpan.FromHours(8), h.Duracion);

        var (inicio, fin) = h.VentanaLocal(new DateOnly(2026, 11, 2));
        Assert.Equal(new DateTime(2026, 11, 2, 22, 0, 0), inicio);
        Assert.Equal(new DateTime(2026, 11, 3, 6, 0, 0), fin);
    }

    [Theory]
    [InlineData(6, 0, 6, 30)]   // 30 min: menos de 1 h
    [InlineData(6, 0, 6, 0)]    // 24 h: más de 12 h
    [InlineData(6, 0, 19, 0)]   // 13 h
    public void Duracion_fuera_de_rango_lanza(int h1, int m1, int h2, int m2) =>
        Lanza.Codigo(HorarioTurno.Errores.Invalido, () => HorarioTurno.Crear(new(h1, m1), new(h2, m2)));

    [Theory]
    [InlineData(6, 14, 13, 21, true)]
    [InlineData(6, 14, 14, 22, false)]
    [InlineData(22, 6, 5, 13, true)]
    [InlineData(22, 6, 6, 14, false)]
    [InlineData(14, 22, 22, 6, false)]
    [InlineData(23, 7, 22, 6, true)]
    public void Traslape_considera_la_medianoche(int a0, int a1, int b0, int b1, bool esperado)
    {
        var a = HorarioTurno.Crear(new(a0, 0), new(a1, 0));
        var b = HorarioTurno.Crear(new(b0, 0), new(b1, 0));
        Assert.Equal(esperado, a.SeTraslapaCon(b));
        Assert.Equal(esperado, b.SeTraslapaCon(a));
    }
}
