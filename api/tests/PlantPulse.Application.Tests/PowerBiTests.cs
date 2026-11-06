using Microsoft.Extensions.Logging.Abstractions;
using PlantPulse.Application.Common;
using PlantPulse.Application.PowerBi;
using PlantPulse.Application.Turnos;
using PlantPulse.Domain.Produccion;
using Xunit;

namespace PlantPulse.Application.Tests;

internal sealed class FakePowerBi : IPowerBiEmbedService
{
    public string? UltimoUsername { get; private set; }
    public string? UltimoReporte { get; private set; }

    public IReadOnlyList<ReportePowerBi> Reportes() => [new("oee", "OEE por línea y turno")];

    public void Validar(string reportKey)
    {
        if (reportKey != "oee") throw new NotFoundException("Reporte de Power BI", reportKey);
    }

    public Task<EmbedPowerBi> GenerarAsync(string reportKey, string usernameRls, CancellationToken ct)
    {
        if (reportKey != "oee") throw new NotFoundException("Reporte de Power BI", reportKey);
        UltimoReporte = reportKey;
        UltimoUsername = usernameRls;
        return Task.FromResult(new EmbedPowerBi(Guid.Empty, "https://app.powerbi.com/reportEmbed?reportId=x", "tok", Mundo.Ahora.AddHours(1)));
    }
}

internal sealed class FakeRefresh : IPowerBiRefreshScheduler
{
    public List<string> Solicitudes { get; } = [];
    public void Solicitar(string motivo) => Solicitudes.Add(motivo);
}

public class IdentidadRlsTests
{
    [Fact]
    public void Ordena_normaliza_y_quita_duplicados() =>
        Assert.Equal("LEO|SIL", IdentidadRls.Crear(["sil", "LEO", " leo "]));

    [Fact]
    public void Sin_plantas_es_403() =>
        Assert.Throws<ForbiddenAccessException>(() => IdentidadRls.Crear([]));
}

public class PoliticaRefreshTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 5, 14, 0, 0, TimeSpan.Zero);
    private static PoliticaRefresh Politica() => new(TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(30), 3);

    [Fact]
    public void Primer_refresh_espera_solo_la_ventana_de_agrupacion() =>
        Assert.Equal(TimeSpan.FromSeconds(60), Politica().CalcularEspera(T0));

    [Fact]
    public void Respeta_el_intervalo_minimo_desde_el_ultimo()
    {
        var p = Politica();
        p.Registrar(T0);
        Assert.Equal(TimeSpan.FromMinutes(20), p.CalcularEspera(T0.AddMinutes(10)));
        Assert.Equal(TimeSpan.FromSeconds(60), p.CalcularEspera(T0.AddMinutes(45)));
    }

    [Fact]
    public void Al_llegar_al_maximo_diario_espera_al_dia_siguiente()
    {
        var p = Politica();
        p.Registrar(T0);
        p.Registrar(T0.AddHours(1));
        p.Registrar(T0.AddHours(2));

        var espera = p.CalcularEspera(T0.AddHours(3));   // 17:00 UTC → mañana 00:00 + 60 s

        Assert.Equal(TimeSpan.FromHours(7) + TimeSpan.FromSeconds(60), espera);
        Assert.Equal(0, p.RefreshesDelDia(T0.AddHours(3) + espera));
    }
}

public class PowerBiHandlersTests
{
    private readonly Mundo _m = new();

    [Fact]
    public async Task Supervisor_recibe_rls_solo_de_su_planta()
    {
        var pbi = new FakePowerBi();
        var dto = await new ObtenerEmbedPowerBiHandler(pbi, _m.Plantas, _m.Supervisor(_m.Leon)).Handle(new("oee", null), default);

        Assert.Equal("LEO", pbi.UltimoUsername);
        Assert.Equal("tok", dto.AccessToken);
    }

    [Fact]
    public async Task Admin_recibe_todas_las_plantas_y_puede_acotar_a_una()
    {
        var pbi = new FakePowerBi();
        var handler = new ObtenerEmbedPowerBiHandler(pbi, _m.Plantas, Mundo.Admin());

        await handler.Handle(new("oee", null), default);
        Assert.Equal("LEO|SIL", pbi.UltimoUsername);

        await handler.Handle(new("oee", _m.Silao.Id), default);
        Assert.Equal("SIL", pbi.UltimoUsername);
    }

    [Fact]
    public async Task Acotar_a_planta_ajena_es_403() =>
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            new ObtenerEmbedPowerBiHandler(new FakePowerBi(), _m.Plantas, _m.Supervisor(_m.Leon)).Handle(new("oee", _m.Silao.Id), default));

    [Fact]
    public async Task Reporte_inexistente_es_404() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ObtenerEmbedPowerBiHandler(new FakePowerBi(), _m.Plantas, Mundo.Admin()).Handle(new("no-existe", null), default));

    [Fact]
    public async Task Cerrar_un_turno_solicita_refresh()
    {
        var refresh = new FakeRefresh();
        var evento = new TurnoLineaCerrado(Guid.CreateVersion7(), _m.Leon.Id, _m.LineaLeon.Id, Mundo.Lunes, Mundo.Ahora);

        await new TurnoLineaCerradoHandler(refresh, NullLogger<TurnoLineaCerradoHandler>.Instance)
            .Handle(new DomainEventNotification<TurnoLineaCerrado>(evento), default);

        Assert.Single(refresh.Solicitudes);
    }
}
