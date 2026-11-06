using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.Produccion;
using PlantPulse.Domain.ValueObjects;
using Xunit;

namespace PlantPulse.Domain.Tests.Produccion;

public class TurnoLineaAperturaTests
{
    [Fact]
    public void Calcula_la_ventana_UTC_con_la_zona_de_la_planta()
    {
        var t = new Escenario().AbrirMatutino();
        Assert.Equal(Escenario.InicioMatutino, t.InicioUtc);
        Assert.Equal(Escenario.FinMatutino, t.FinUtc);
        Assert.Equal(480m, t.DuracionTurno.TotalMinutos);
        Assert.Equal(EstadoTurno.Abierto, t.Estado);
    }

    [Fact]
    public void Turno_nocturno_pertenece_a_la_fecha_de_inicio()
    {
        var e = new Escenario();
        var t = TurnoLinea.Abrir(e.Planta, e.Linea, e.Nocturno.Id, Escenario.Lunes, Escenario.Utc(2026, 11, 3, 12, 30));
        Assert.Equal(Escenario.Lunes, t.FechaOperativa);
        Assert.Equal(Escenario.Utc(2026, 11, 3, 4, 0), t.InicioUtc);   // lunes 22:00 local
        Assert.Equal(Escenario.Utc(2026, 11, 3, 12, 0), t.FinUtc);     // martes 06:00 local
    }

    [Fact]
    public void Fecha_futura_lanza()
    {
        var e = new Escenario();
        Lanza.Codigo(TurnoLinea.Errores.FechaFutura,
            () => TurnoLinea.Abrir(e.Planta, e.Linea, e.Matutino.Id, Escenario.Martes, Escenario.DespuesDelMatutino));
    }

    [Fact]
    public void Hasta_siete_dias_atras_se_permite()
    {
        var e = new Escenario();
        // 2026-11-09 12:00 local → hoy = 9; lunes 2 está exactamente 7 días atrás.
        var t = TurnoLinea.Abrir(e.Planta, e.Linea, e.Matutino.Id, Escenario.Lunes, Escenario.Utc(2026, 11, 9, 18, 0));
        Assert.Equal(Escenario.Lunes, t.FechaOperativa);
    }

    [Fact]
    public void Mas_de_siete_dias_atras_lanza()
    {
        var e = new Escenario();
        Lanza.Codigo(TurnoLinea.Errores.FueraDeVentanaCaptura,
            () => TurnoLinea.Abrir(e.Planta, e.Linea, e.Matutino.Id, Escenario.Lunes, Escenario.Utc(2026, 11, 10, 18, 0)));
    }

    [Fact]
    public void Turno_que_no_ha_iniciado_lanza()
    {
        var e = new Escenario();
        Lanza.Codigo(TurnoLinea.Errores.TurnoNoIniciado, () => e.AbrirMatutino(Escenario.InicioMatutino.AddMinutes(-1)));
    }

    [Fact]
    public void Linea_de_otra_planta_lanza()
    {
        var e = new Escenario();
        var otra = Planta.Crear("SIL", "Silao", "America/Mexico_City");
        var lineaAjena = Linea.Crear(otra, "SIL-L01", "Ensamble", TiempoCiclo.DeSegundos(30m));
        Lanza.Codigo(TurnoLinea.Errores.LineaDePlantaDistinta,
            () => TurnoLinea.Abrir(e.Planta, lineaAjena, e.Matutino.Id, Escenario.Lunes, Escenario.DespuesDelMatutino));
    }

    [Fact]
    public void Turno_de_otra_planta_lanza()
    {
        var e = new Escenario();
        Lanza.Codigo(Planta.Errores.TurnoNoExiste,
            () => TurnoLinea.Abrir(e.Planta, e.Linea, Guid.CreateVersion7(), Escenario.Lunes, Escenario.DespuesDelMatutino));
    }

    [Fact]
    public void Linea_inactiva_lanza()
    {
        var e = new Escenario();
        e.Linea.Desactivar();
        Lanza.Codigo(TurnoLinea.Errores.LineaInactiva, () => e.AbrirMatutino());
    }
}

public class TurnoLineaParosTests
{
    private readonly Escenario _e = new();
    private static readonly DateTimeOffset Ahora = Escenario.DespuesDelMatutino;

    private static DateTimeOffset H(int hora, int minuto) => Escenario.Utc(2026, 11, 2, hora, minuto);

    [Fact]
    public void Registra_y_copia_el_tipo_de_la_causa()
    {
        var t = _e.AbrirMatutino();
        var paro = t.RegistrarParo(_e.Comida, H(16, 0), H(16, 30), "  Comedor  ", Ahora);

        Assert.Equal(TipoParo.Planificado, paro.Tipo);
        Assert.Equal(30m, paro.Duracion.TotalMinutos);
        Assert.Equal("Comedor", paro.Comentario);
        Assert.Single(t.Paros);
    }

    [Fact]
    public void Que_cruza_el_fin_del_turno_lanza() =>
        Lanza.Codigo(TurnoLinea.Errores.ParoFueraDeTurno,
            () => _e.AbrirMatutino().RegistrarParo(_e.FallaMecanica, H(19, 50), H(20, 10), null, Escenario.FinMatutino.AddHours(1)));

    [Fact]
    public void Que_empieza_antes_del_turno_lanza() =>
        Lanza.Codigo(TurnoLinea.Errores.ParoFueraDeTurno,
            () => _e.AbrirMatutino().RegistrarParo(_e.FallaMecanica, H(11, 55), H(12, 10), null, Ahora));

    [Fact]
    public void Fin_igual_o_antes_del_inicio_lanza() =>
        Lanza.Codigo(TurnoLinea.Errores.ParoInvalido,
            () => _e.AbrirMatutino().RegistrarParo(_e.FallaMecanica, H(13, 0), H(13, 0), null, Ahora));

    [Fact]
    public void Que_termina_en_el_futuro_lanza()
    {
        var t = _e.AbrirMatutino(H(15, 0));
        Lanza.Codigo(TurnoLinea.Errores.ParoFuturo, () => t.RegistrarParo(_e.FallaMecanica, H(14, 30), H(15, 30), null, H(15, 0)));
    }

    [Fact]
    public void Traslapado_lanza()
    {
        var t = _e.AbrirMatutino();
        t.RegistrarParo(_e.FallaMecanica, H(13, 0), H(13, 30), null, Ahora);
        Lanza.Codigo(TurnoLinea.Errores.ParoTraslapado, () => t.RegistrarParo(_e.FaltaMaterial, H(13, 20), H(13, 40), null, Ahora));
    }

    [Fact]
    public void Contiguo_se_permite()
    {
        var t = _e.AbrirMatutino();
        t.RegistrarParo(_e.FallaMecanica, H(13, 0), H(13, 30), null, Ahora);
        t.RegistrarParo(_e.FaltaMaterial, H(13, 30), H(13, 40), null, Ahora);
        Assert.Equal(2, t.Paros.Count);
    }

    [Fact]
    public void Causa_inactiva_lanza()
    {
        _e.FallaMecanica.Desactivar();
        Lanza.Codigo(TurnoLinea.Errores.CausaInactiva,
            () => _e.AbrirMatutino().RegistrarParo(_e.FallaMecanica, H(13, 0), H(13, 30), null, Ahora));
    }

    [Fact]
    public void Comentario_demasiado_largo_lanza() =>
        Lanza.Codigo(TurnoLinea.Errores.ParoInvalido,
            () => _e.AbrirMatutino().RegistrarParo(_e.FallaMecanica, H(13, 0), H(13, 30), new string('x', 501), Ahora));

    [Fact]
    public void Eliminar_paro()
    {
        var t = _e.AbrirMatutino();
        var paro = t.RegistrarParo(_e.FallaMecanica, H(13, 0), H(13, 30), null, Ahora);
        t.EliminarParo(paro.Id);
        Assert.Empty(t.Paros);
        Lanza.Codigo(TurnoLinea.Errores.ParoNoExiste, () => t.EliminarParo(paro.Id));
    }
}

public class TurnoLineaProduccionTests
{
    private readonly Escenario _e = new();
    private static readonly DateTimeOffset Ahora = Escenario.DespuesDelMatutino;

    [Fact]
    public void Registra_con_el_ciclo_estandar_de_la_linea()
    {
        var t = _e.AbrirMatutino();
        var r = t.RegistrarProduccion(_e.Orden, _e.Linea, 500, 490, Ahora);
        Assert.Equal(45m, r.TiempoCicloIdeal.Segundos);
        Assert.Equal(10, r.Scrap);
        Assert.Equal(22_500m, r.SegundosIdeales);
    }

    [Fact]
    public void Cambiar_el_estandar_despues_no_altera_lo_capturado()
    {
        var t = _e.AbrirMatutino();
        var r = t.RegistrarProduccion(_e.Orden, _e.Linea, 100, 100, Ahora);
        _e.Linea.CambiarTiempoCicloEstandar(TiempoCiclo.DeSegundos(30m));
        Assert.Equal(45m, r.TiempoCicloIdeal.Segundos);
    }

    [Fact]
    public void Orden_de_otra_linea_lanza()
    {
        var otraLinea = Linea.Crear(_e.Planta, "LEO-L02", "Fundición 2", TiempoCiclo.DeSegundos(50m));
        var ordenAjena = OrdenProduccion.Crear("2000001", otraLinea, "M", "Mat", 10, Escenario.Lunes, Escenario.Lunes);
        Lanza.Codigo(TurnoLinea.Errores.OrdenDeOtraLinea,
            () => _e.AbrirMatutino().RegistrarProduccion(ordenAjena, _e.Linea, 10, 10, Ahora));
    }

    [Fact]
    public void Misma_orden_dos_veces_en_el_turno_lanza()
    {
        var t = _e.AbrirMatutino();
        t.RegistrarProduccion(_e.Orden, _e.Linea, 100, 95, Ahora);
        Lanza.Codigo(TurnoLinea.Errores.OrdenYaRegistrada, () => t.RegistrarProduccion(_e.Orden, _e.Linea, 50, 50, Ahora));
    }

    [Fact]
    public void Orden_terminada_lanza()
    {
        _e.Orden.Terminar();
        Lanza.Codigo(TurnoLinea.Errores.OrdenNoAceptaProduccion,
            () => _e.AbrirMatutino().RegistrarProduccion(_e.Orden, _e.Linea, 10, 10, Ahora));
    }

    [Fact]
    public void Buenas_mayores_que_totales_lanza() =>
        Lanza.Codigo(ConteoPiezas.Errores.Invalido,
            () => _e.AbrirMatutino().RegistrarProduccion(_e.Orden, _e.Linea, 10, 11, Ahora));

    [Fact]
    public void Corregir_produccion()
    {
        var t = _e.AbrirMatutino();
        var r = t.RegistrarProduccion(_e.Orden, _e.Linea, 100, 95, Ahora);
        t.CorregirProduccion(r.Id, 120, 110, Ahora.AddMinutes(5));
        Assert.Equal(120, r.PiezasTotales);
        Assert.Equal(110, r.PiezasBuenas);
        Assert.Equal(Ahora.AddMinutes(5), r.ModificadoEnUtc);
        Lanza.Codigo(TurnoLinea.Errores.RegistroNoExiste, () => t.CorregirProduccion(Guid.CreateVersion7(), 1, 1, Ahora));
    }
}

public class TurnoLineaCicloDeVidaTests
{
    private readonly Escenario _e = new();

    [Fact]
    public void Cerrar_antes_de_que_termine_lanza()
    {
        var t = _e.AbrirMatutino(Escenario.InicioMatutino.AddHours(1));
        Lanza.Codigo(TurnoLinea.Errores.TurnoNoTerminado, () => t.Cerrar(Escenario.FinMatutino.AddMinutes(-1)));
    }

    [Fact]
    public void Cerrar_levanta_evento_y_deja_el_turno_inmutable()
    {
        var t = _e.AbrirMatutino();
        var r = t.RegistrarProduccion(_e.Orden, _e.Linea, 100, 100, Escenario.DespuesDelMatutino);
        var paro = t.RegistrarParo(_e.Comida, Escenario.InicioMatutino.AddHours(4), Escenario.InicioMatutino.AddHours(4.5), null, Escenario.DespuesDelMatutino);

        t.Cerrar(Escenario.DespuesDelMatutino);

        Assert.Equal(EstadoTurno.Cerrado, t.Estado);
        Assert.Equal(Escenario.DespuesDelMatutino, t.CerradoEnUtc);
        var evento = Assert.IsType<TurnoLineaCerrado>(Assert.Single(t.DomainEvents));
        Assert.Equal(t.PlantaId, evento.PlantaId);

        var otraOrden = OrdenProduccion.Crear("1000002", _e.Linea, "M", "Mat", 10, Escenario.Lunes, Escenario.Lunes);
        Lanza.Codigo(TurnoLinea.Errores.TurnoCerrado, () => t.RegistrarProduccion(otraOrden, _e.Linea, 1, 1, Escenario.DespuesDelMatutino));
        Lanza.Codigo(TurnoLinea.Errores.TurnoCerrado, () => t.CorregirProduccion(r.Id, 1, 1, Escenario.DespuesDelMatutino));
        Lanza.Codigo(TurnoLinea.Errores.TurnoCerrado, () => t.RegistrarParo(_e.FallaMecanica, Escenario.InicioMatutino, Escenario.InicioMatutino.AddMinutes(5), null, Escenario.DespuesDelMatutino));
        Lanza.Codigo(TurnoLinea.Errores.TurnoCerrado, () => t.EliminarParo(paro.Id));
        Lanza.Codigo(TurnoLinea.Errores.TurnoCerrado, () => t.Cerrar(Escenario.DespuesDelMatutino));
    }

    [Fact]
    public void Reabrir_permite_cambios_y_levanta_evento()
    {
        var t = _e.AbrirMatutino();
        t.Cerrar(Escenario.DespuesDelMatutino);
        t.ClearDomainEvents();

        t.Reabrir(Escenario.DespuesDelMatutino.AddHours(1));

        Assert.Equal(EstadoTurno.Abierto, t.Estado);
        Assert.Null(t.CerradoEnUtc);
        Assert.IsType<TurnoLineaReabierto>(Assert.Single(t.DomainEvents));
        t.RegistrarProduccion(_e.Orden, _e.Linea, 10, 10, Escenario.DespuesDelMatutino.AddHours(1));
    }

    [Fact]
    public void Reabrir_si_no_esta_cerrado_lanza() =>
        Lanza.Codigo(TurnoLinea.Errores.TurnoNoCerrado, () => _e.AbrirMatutino().Reabrir(Escenario.DespuesDelMatutino));
}

public class TurnoLineaOeeTests
{
    private readonly Escenario _e = new();
    private static readonly DateTimeOffset Ahora = Escenario.DespuesDelMatutino;
    private static DateTimeOffset H(int hora, int minuto) => Escenario.Utc(2026, 11, 2, hora, minuto);

    [Fact]
    public void Calcula_el_oee_del_turno_con_paros_y_produccion()
    {
        var t = _e.AbrirMatutino();
        t.RegistrarParo(_e.Comida, H(16, 0), H(16, 30), null, Ahora);           // 30 planificado
        t.RegistrarParo(_e.FallaMecanica, H(13, 0), H(13, 30), null, Ahora);    // 30 no planificado
        t.RegistrarParo(_e.FaltaMaterial, H(18, 0), H(18, 15), null, Ahora);    // 15 no planificado
        t.RegistrarProduccion(_e.Orden, _e.Linea, 486, 474, Ahora);             // 45 s × 486

        var oee = t.CalcularOee();

        Assert.Equal(30m, oee.ParosPlanificados.TotalMinutos);
        Assert.Equal(45m, oee.ParosNoPlanificados.TotalMinutos);
        Assert.Equal(0.9m, oee.Disponibilidad.Valor);
        Assert.Equal(0.9m, oee.Desempeno.Valor);
        Assert.Equal(0.79m, Math.Round(oee.Oee.Valor, 10));
        Assert.Equal(12, oee.Scrap);
    }

    [Fact]
    public void Suma_varias_ordenes_con_su_propio_ciclo()
    {
        var t = _e.AbrirMatutino();
        var orden2 = OrdenProduccion.Crear("1000002", _e.Linea, "SOP-1180", "Soporte", 500, Escenario.Lunes, Escenario.Lunes, TiempoCiclo.DeSegundos(60m));
        t.RegistrarProduccion(_e.Orden, _e.Linea, 320, 316, Ahora);   // 45 × 320 = 14 400 s
        t.RegistrarProduccion(orden2, _e.Linea, 120, 118, Ahora);     // 60 × 120 =  7 200 s

        var oee = t.CalcularOee();

        Assert.Equal(21_600m, oee.SegundosIdeales);
        Assert.Equal(0.75m, oee.Desempeno.Valor);                      // 21 600 / 28 800
        Assert.Equal(440, oee.Piezas.Totales);
        Assert.Equal(434, oee.Piezas.Buenas);
    }

    [Fact]
    public void Turno_sin_capturas_tiene_oee_cero_y_disponibilidad_completa()
    {
        var oee = _e.AbrirMatutino().CalcularOee();
        Assert.Equal(Porcentaje.Cien, oee.Disponibilidad);
        Assert.Equal(Porcentaje.Cero, oee.Desempeno);
        Assert.Equal(Porcentaje.Cero, oee.Oee);
    }
}
