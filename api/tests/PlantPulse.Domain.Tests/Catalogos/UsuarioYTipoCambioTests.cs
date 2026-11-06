using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.TiposCambio;
using PlantPulse.Domain.Usuarios;
using PlantPulse.Domain.ValueObjects;
using Xunit;

namespace PlantPulse.Domain.Tests.Catalogos;

public class UsuarioTests
{
    private static readonly Guid Leon = Guid.CreateVersion7();
    private static readonly Guid Silao = Guid.CreateVersion7();

    [Fact]
    public void Normaliza_email_y_controla_el_acceso_por_planta()
    {
        var u = Usuario.Crear(" Supervisor.Leon@PlantPulse.mx ", "Ana", "hash", [Roles.Supervisor], [Leon]);
        Assert.Equal("supervisor.leon@plantpulse.mx", u.Email);
        Assert.True(u.PuedeAccederPlanta(Leon));
        Assert.False(u.PuedeAccederPlanta(Silao));
        Assert.False(u.EsAdmin);
    }

    [Fact]
    public void Admin_accede_a_todas_las_plantas_sin_lista()
    {
        var u = Usuario.Crear("admin@plantpulse.mx", "Admin", "hash", [Roles.Admin], []);
        Assert.True(u.EsAdmin);
        Assert.True(u.PuedeAccederPlanta(Silao));
    }

    [Fact]
    public void Rol_desconocido_lanza() =>
        Lanza.Codigo(Usuario.Errores.RolInvalido, () => Usuario.Crear("a@b.mx", "A", "h", ["Root"], [Leon]));

    [Fact]
    public void Sin_roles_lanza() =>
        Lanza.Codigo(Usuario.Errores.RolInvalido, () => Usuario.Crear("a@b.mx", "A", "h", [], [Leon]));

    [Fact]
    public void No_admin_sin_plantas_lanza() =>
        Lanza.Codigo(Usuario.Errores.SinPlantas, () => Usuario.Crear("a@b.mx", "A", "h", [Roles.Consulta], []));

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("@dominio.mx")]
    [InlineData("usuario@")]
    public void Email_invalido_lanza(string email) =>
        Lanza.Codigo(Usuario.Errores.DatoInvalido, () => Usuario.Crear(email, "A", "h", [Roles.Admin], []));
}

public class TipoCambioTests
{
    private static readonly DateTimeOffset Ahora = Escenario.Utc(2026, 11, 4, 18, 0);

    private static TipoCambio Tc(int dia, decimal valor) =>
        TipoCambio.Crear(TipoCambio.SerieFix, new DateOnly(2026, 10, dia), valor, "Banxico", Ahora);

    [Fact]
    public void Fuera_de_rango_lanza() =>
        Lanza.Codigo(TipoCambio.Errores.Invalido, () => Tc(1, 0m));

    [Fact]
    public void Vigente_usa_el_del_dia_o_el_ultimo_anterior()
    {
        // Viernes 30 y lunes 2 de nov no está; sábado 31 usa el viernes.
        var lista = new[] { Tc(29, 18.10m), Tc(30, 18.20m) };
        Assert.Equal(18.20m, TipoCambio.VigentePara(new DateOnly(2026, 10, 31), lista)!.PesosPorDolar);
        Assert.Equal(18.10m, TipoCambio.VigentePara(new DateOnly(2026, 10, 29), lista)!.PesosPorDolar);
        Assert.Equal(18.10m, TipoCambio.VigentePara(new DateOnly(2026, 10, 1), lista)!.PesosPorDolar);
        Assert.Null(TipoCambio.VigentePara(new DateOnly(2026, 10, 1), []));
    }
}

public class OrdenProduccionPlanTests
{
    [Fact]
    public void ActualizarPlan_indica_si_cambio()
    {
        var e = new Escenario();
        Assert.False(e.Orden.ActualizarPlan(1500, Escenario.Lunes, Escenario.Lunes.AddDays(2), null, null));
        Assert.True(e.Orden.ActualizarPlan(1600, Escenario.Lunes, Escenario.Lunes.AddDays(3), TiempoCiclo.DeSegundos(44m), 85.5m));
        Assert.Equal(1600, e.Orden.CantidadPlaneada);
        Assert.Equal(85.5m, e.Orden.CostoUnitarioMxn);
    }

    [Fact]
    public void ActualizarPlan_de_orden_terminada_lanza()
    {
        var e = new Escenario();
        e.Orden.Terminar();
        Lanza.Codigo(OrdenProduccion.Errores.TransicionInvalida,
            () => e.Orden.ActualizarPlan(10, Escenario.Lunes, Escenario.Lunes, null, null));
    }

    [Fact]
    public void Costo_negativo_lanza()
    {
        var e = new Escenario();
        Lanza.Codigo(OrdenProduccion.Errores.DatoInvalido, () => e.Orden.AsignarCostoEstandar(-1m));
    }

    [Fact]
    public void Costo_se_puede_asignar_a_una_orden_terminada()
    {
        var e = new Escenario();
        e.Orden.Terminar();
        e.Orden.AsignarCostoEstandar(120m);
        Assert.Equal(120m, e.Orden.CostoUnitarioMxn);
    }
}
