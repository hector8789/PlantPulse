using Microsoft.Extensions.Logging.Abstractions;
using PlantPulse.Application.Auth;
using PlantPulse.Application.Common;
using PlantPulse.Application.Erp;
using PlantPulse.Application.Kpis;
using PlantPulse.Application.Turnos;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Produccion;
using PlantPulse.Domain.TiposCambio;
using PlantPulse.Domain.Usuarios;
using Xunit;

namespace PlantPulse.Application.Tests;

public class TurnoHandlersTests
{
    private readonly Mundo _m = new();

    private AbrirTurnoHandler Abrir(ICurrentUser u) =>
        new(_m.Plantas, _m.Lineas, _m.Turnos, _m.Uow, u, _m.Reloj, _m.Dtos);

    [Fact]
    public async Task Abrir_turno_guarda_y_devuelve_el_detalle()
    {
        var dto = await Abrir(_m.Supervisor(_m.Leon)).Handle(new(_m.LineaLeon.Id, _m.Matutino.Id, Mundo.Lunes), default);

        Assert.Equal("LEO-L01", dto.LineaCodigo);
        Assert.Equal(1, dto.TurnoNumero);
        Assert.Equal("Abierto", dto.Estado);
        Assert.Single(_m.Turnos.Lista);
        Assert.Equal(1, _m.Uow.Guardados);
    }

    [Fact]
    public async Task Abrir_turno_en_planta_ajena_es_403()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            Abrir(_m.Supervisor(_m.Silao)).Handle(new(_m.LineaLeon.Id, _m.Matutino.Id, Mundo.Lunes), default));
        Assert.Empty(_m.Turnos.Lista);
    }

    [Fact]
    public async Task Abrir_turno_duplicado_es_409()
    {
        _m.TurnoMatutinoAbierto();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Abrir(_m.Supervisor(_m.Leon)).Handle(new(_m.LineaLeon.Id, _m.Matutino.Id, Mundo.Lunes), default));
        Assert.Equal("turno_linea.ya_existe", ex.Codigo);
    }

    [Fact]
    public async Task Abrir_turno_de_linea_inexistente_es_404() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Abrir(Mundo.Admin()).Handle(new(Guid.CreateVersion7(), _m.Matutino.Id, Mundo.Lunes), default));

    [Fact]
    public async Task Registrar_produccion_inicia_la_orden_y_recalcula_oee()
    {
        var turno = _m.TurnoMatutinoAbierto();
        var handler = new RegistrarProduccionHandler(_m.Turnos, _m.Lineas, _m.Ordenes, _m.Uow, _m.Supervisor(_m.Leon), _m.Reloj, _m.Dtos);

        var dto = await handler.Handle(new(turno.Id, _m.OrdenLeon.Id, 600, 588), default);

        Assert.Equal(EstadoOrden.EnProceso, _m.OrdenLeon.Estado);
        var registro = Assert.Single(dto.Registros);
        Assert.Equal("1000001", registro.OrdenNumero);
        Assert.Equal(12, dto.Oee.Scrap);
        Assert.Equal(0.9375m, dto.Oee.Desempeno);   // 45 s × 600 / 28 800 s
    }

    [Fact]
    public async Task Registrar_paro_devuelve_causa_y_duracion()
    {
        var turno = _m.TurnoMatutinoAbierto();
        var handler = new RegistrarParoHandler(_m.Turnos, _m.Causas, _m.Uow, _m.Supervisor(_m.Leon), _m.Reloj, _m.Dtos);

        var dto = await handler.Handle(new(turno.Id, _m.FallaMecanica.Id, turno.InicioUtc.AddHours(1), turno.InicioUtc.AddHours(1.5), "Bomba"), default);

        var paro = Assert.Single(dto.Paros);
        Assert.Equal("NP-MEC", paro.CausaCodigo);
        Assert.Equal("NoPlanificado", paro.Tipo);
        Assert.Equal(30m, paro.DuracionMin);
    }

    [Fact]
    public async Task Cerrar_turno_lo_deja_cerrado()
    {
        var turno = _m.TurnoMatutinoAbierto();
        var handler = new CerrarTurnoHandler(_m.Turnos, _m.Uow, _m.Supervisor(_m.Leon), _m.Reloj, _m.Dtos, NullLogger<CerrarTurnoHandler>.Instance);

        var dto = await handler.Handle(new(turno.Id), default);

        Assert.Equal("Cerrado", dto.Estado);
        Assert.IsType<TurnoLineaCerrado>(Assert.Single(turno.DomainEvents));
    }

    [Fact]
    public async Task Reabrir_sin_ser_admin_es_403()
    {
        var turno = _m.TurnoMatutinoAbierto();
        turno.Cerrar(Mundo.Ahora);
        var handler = new ReabrirTurnoHandler(_m.Turnos, _m.Uow, _m.Supervisor(_m.Leon), _m.Reloj, _m.Dtos);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new(turno.Id), default));
    }

    [Fact]
    public async Task Listar_turnos_solo_devuelve_plantas_visibles()
    {
        _m.TurnoMatutinoAbierto();
        var handler = new ListarTurnosHandler(_m.Turnos, _m.Lineas, _m.Plantas, _m.Supervisor(_m.Silao));
        var lista = await handler.Handle(new(null, null, Mundo.Lunes, Mundo.Lunes), default);
        Assert.Empty(lista);

        var admin = new ListarTurnosHandler(_m.Turnos, _m.Lineas, _m.Plantas, Mundo.Admin());
        Assert.Single(await admin.Handle(new(null, null, Mundo.Lunes, Mundo.Lunes), default));
    }
}

public class LoginHandlerTests
{
    private readonly Mundo _m = new();

    private LoginCommandHandler Handler(FakeTokens tokens, params Usuario[] usuarios) =>
        new(new FakeUsuarios(usuarios), _m.Plantas, new FakeHasher(), tokens);

    [Fact]
    public async Task Password_incorrecto_es_401()
    {
        var u = Usuario.Crear("sup@plantpulse.mx", "Sup", "hash:correcta", [Roles.Supervisor], [_m.Leon.Id]);
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Handler(new FakeTokens(), u).Handle(new("sup@plantpulse.mx", "otra"), default));
    }

    [Fact]
    public async Task Usuario_inexistente_es_401_con_el_mismo_mensaje()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Handler(new FakeTokens()).Handle(new("nadie@plantpulse.mx", "x"), default));
        Assert.Equal("Email o contraseña incorrectos.", ex.Message);
    }

    [Fact]
    public async Task Email_se_normaliza_y_supervisor_recibe_sus_plantas()
    {
        var tokens = new FakeTokens();
        var u = Usuario.Crear("sup@plantpulse.mx", "Sup", "hash:clave", [Roles.Supervisor], [_m.Leon.Id]);

        var r = await Handler(tokens, u).Handle(new("  SUP@PlantPulse.mx ", "clave"), default);

        Assert.Equal("token-sup@plantpulse.mx", r.AccessToken);
        Assert.Equal(_m.Leon.Id, Assert.Single(r.User.PlantaIds));
        Assert.Equal(Roles.Supervisor, Assert.Single(r.User.Roles));
    }

    [Fact]
    public async Task Admin_recibe_todas_las_plantas()
    {
        var tokens = new FakeTokens();
        var admin = Usuario.Crear("admin@plantpulse.mx", "Admin", "hash:clave", [Roles.Admin], []);

        var r = await Handler(tokens, admin).Handle(new("admin@plantpulse.mx", "clave"), default);

        Assert.Equal(2, r.User.PlantaIds.Count);
        Assert.Equal(2, tokens.UltimasPlantas!.Count);
    }
}

public class KpiHandlersTests
{
    private readonly Mundo _m = new();

    [Fact]
    public void Oee_agregado_suma_tiempos_en_lugar_de_promediar()
    {
        // Turno A: sin paros, desempeño 100%. Turno B: 240 min de paro no planificado, sin piezas.
        var a = _m.TurnoMatutinoAbierto();
        a.RegistrarProduccion(_m.OrdenLeon, _m.LineaLeon, 640, 640, Mundo.Ahora);   // 45 × 640 = 28 800 s
        var b = TurnoLinea.Abrir(_m.Leon, _m.LineaLeon, _m.Leon.Turnos.Single(t => t.Numero == 2).Id, Mundo.Lunes, Mundo.Ahora.AddHours(9));
        b.RegistrarParo(_m.FallaMecanica, b.InicioUtc, b.InicioUtc.AddHours(4), null, Mundo.Ahora.AddHours(9));

        var r = ObtenerOeeHandler.Agregar([a, b]);

        // Disponibilidad = (480 + 240) / 960 = 0.75 ; Desempeño = 28 800 / 43 200 = 0.6667 ; Calidad = 1.
        Assert.Equal(0.75m, r.Disponibilidad.Valor);
        Assert.Equal(0.6667m, Math.Round(r.Desempeno.Valor, 4));
        Assert.Equal(0.5m, Math.Round(r.Oee.Valor, 10));
    }

    [Fact]
    public async Task Costo_de_scrap_usa_el_fix_vigente_y_reporta_piezas_sin_costo()
    {
        var turno = _m.TurnoMatutinoAbierto();
        var sinCosto = OrdenProduccion.Crear("1000002", _m.LineaLeon, "X-1", "Sin costo", 100, Mundo.Lunes, Mundo.Lunes);
        _m.Ordenes.Lista.Add(sinCosto);
        turno.RegistrarProduccion(_m.OrdenLeon, _m.LineaLeon, 500, 490, Mundo.Ahora);   // 10 × 400 MXN = 4 000 MXN
        turno.RegistrarProduccion(sinCosto, _m.LineaLeon, 50, 45, Mundo.Ahora);         // 5 piezas sin costo

        // El FIX publicado más reciente antes del lunes 2 es el del viernes 30 de octubre.
        var banxico = new FakeBanxico(new TipoCambioPublicado(new DateOnly(2026, 10, 29), 19.90m), new TipoCambioPublicado(new DateOnly(2026, 10, 30), 20.00m));
        var handler = new ObtenerCostoScrapHandler(_m.Turnos, _m.Lineas, _m.Ordenes, _m.ServicioTiposCambio(banxico), _m.Supervisor(_m.Leon));

        var r = await handler.Handle(new(null, null, Mundo.Lunes, Mundo.Lunes), default);

        Assert.Equal(15, r.PiezasScrap);
        Assert.Equal(5, r.PiezasScrapSinCosto);
        Assert.Equal(4000m, r.CostoMxn);
        Assert.Equal(200m, r.CostoUsd);
        Assert.Equal(20.00m, Assert.Single(r.PorDia).TipoCambio);
        Assert.Equal(2, _m.TiposCambio.Lista.Count);   // se guardaron en caché
        Assert.False(r.TipoCambioDesactualizado);
    }

    [Fact]
    public async Task Tipos_de_cambio_usa_la_cache_si_cubre_el_rango()
    {
        _m.TiposCambio.Lista.Add(TipoCambio.Crear(TipoCambio.SerieFix, new DateOnly(2026, 10, 20), 19.5m, "Banxico", Mundo.Ahora));
        _m.TiposCambio.Lista.Add(TipoCambio.Crear(TipoCambio.SerieFix, new DateOnly(2026, 11, 2), 20.1m, "Banxico", Mundo.Ahora));
        var banxico = new FakeBanxico();

        var r = await _m.ServicioTiposCambio(banxico).ObtenerAsync(new DateOnly(2026, 10, 25), Mundo.Lunes, default);

        Assert.Equal(0, banxico.Llamadas);
        Assert.Equal(2, r.Tipos.Count);
    }

    [Fact]
    public async Task Si_Banxico_falla_usa_lo_guardado_y_lo_marca_desactualizado()
    {
        // Hay un dato guardado, pero no llega al final del rango: se intenta Banxico y falla.
        _m.TiposCambio.Lista.Add(TipoCambio.Crear(TipoCambio.SerieFix, new DateOnly(2026, 10, 20), 19.5m, "Banxico", Mundo.Ahora));
        var banxico = new FakeBanxico { Falla = true };

        var r = await _m.ServicioTiposCambio(banxico).ObtenerAsync(new DateOnly(2026, 10, 25), Mundo.Lunes, default);

        Assert.True(r.Desactualizado);
        Assert.Single(r.Tipos);
    }

    [Fact]
    public async Task Si_Banxico_falla_sin_cache_es_503() =>
        await Assert.ThrowsAsync<ExternalServiceException>(() =>
            _m.ServicioTiposCambio(new FakeBanxico { Falla = true }).ObtenerAsync(Mundo.Lunes, Mundo.Lunes, default));
}

public class SincronizarOrdenesTests
{
    private readonly Mundo _m = new();

    private static OrdenErp Erp(string numero, string centro, int cantidad = 1000, string estado = "REL", string planta = "LEO") =>
        new(numero, planta, centro, "CAR-4521", "Carcasa", cantidad, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 11), null, 412.5m, estado);

    private SincronizarOrdenesHandler Handler(FakeErp erp, ICurrentUser u) =>
        new(_m.Plantas, _m.Lineas, _m.Ordenes, erp, _m.Uow, u, _m.Reloj, NullLogger<SincronizarOrdenesHandler>.Instance);

    [Fact]
    public async Task Crea_actualiza_y_omite_con_motivo()
    {
        var erp = new FakeErp(
            Erp("4000001", "LEO-L01"),                       // nueva
            Erp("1000001", "LEO-L01", cantidad: 1800),       // existente con otra cantidad → actualizada
            Erp("4000002", "LEO-L09"),                       // centro inexistente → omitida
            Erp("4000003", "LEO-L01", estado: "TECO"),       // nueva pero ya cerrada → omitida
            Erp("4000004", "SIL-L01", planta: "SIL"));       // otra planta → omitida

        var r = await Handler(erp, _m.Supervisor(_m.Leon)).Handle(new(_m.Leon.Id), default);

        Assert.Equal(5, r.Recibidas);
        Assert.Equal(1, r.Creadas);
        Assert.Equal(1, r.Actualizadas);
        Assert.Equal(3, r.Omitidas.Count);
        Assert.Equal(1800, _m.OrdenLeon.CantidadPlaneada);
        Assert.Contains(_m.Ordenes.Lista, o => o.Numero == "4000001" && o.CostoUnitarioMxn == 412.5m);
        Assert.Equal(1, _m.Uow.Guardados);
    }

    [Fact]
    public async Task Repetir_sin_cambios_no_actualiza()
    {
        var erp = new FakeErp(Erp("4000001", "LEO-L01"));
        await Handler(erp, _m.Supervisor(_m.Leon)).Handle(new(_m.Leon.Id), default);
        var r = await Handler(erp, _m.Supervisor(_m.Leon)).Handle(new(_m.Leon.Id), default);

        Assert.Equal(0, r.Creadas);
        Assert.Equal(0, r.Actualizadas);
        Assert.Equal(1, r.SinCambios);
    }

    [Fact]
    public async Task Teco_en_el_erp_termina_la_orden_existente()
    {
        var erp = new FakeErp(new OrdenErp("1000001", "LEO", "LEO-L01", "CAR-4521", "Carcasa", 1500,
            Mundo.Lunes, Mundo.Lunes.AddDays(2), null, 400m, "TECO"));

        var r = await Handler(erp, _m.Supervisor(_m.Leon)).Handle(new(_m.Leon.Id), default);

        Assert.Equal(1, r.Actualizadas);
        Assert.Equal(EstadoOrden.Terminada, _m.OrdenLeon.Estado);
    }

    [Fact]
    public async Task Planta_ajena_es_403() =>
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            Handler(new FakeErp(), _m.Supervisor(_m.Silao)).Handle(new(_m.Leon.Id), default));
}
