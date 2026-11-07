using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using PlantPulse.Domain.Produccion;
using PlantPulse.Infrastructure.Persistence;
using Xunit;

namespace PlantPulse.Api.IntegrationTests.Persistence;

/// <summary>
/// Valida el modelo de EF Core sin abrir conexión: construir el modelo y generar el script DDL
/// detecta errores de mapeo antes de crear la migración.
/// </summary>
public partial class ModeloEfTests
{
    private static PlantPulseDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<PlantPulseDbContext>()
            .UseNpgsql("Host=localhost;Database=plantpulse_modelo;Username=x;Password=x")
            .UseSnakeCaseNamingConvention()
            .Options);

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex SnakeCase();

    [Fact]
    public void Tablas_esperadas_en_snake_case()
    {
        using var ctx = CrearContexto();
        var tablas = ctx.GetService<IDesignTimeModel>().Model.GetEntityTypes()
            .Select(e => e.GetTableName()).OrderBy(n => n).ToList();

        Assert.Equal(
            new[] { "causas_paro", "lineas", "ordenes_produccion", "plantas", "powerbi_refresh_estado", "registros_paro", "registros_produccion", "tipos_cambio", "turnos", "turnos_linea", "usuarios" },
            tablas.ToArray());
    }

    [Fact]
    public void Columnas_en_snake_case()
    {
        using var ctx = CrearContexto();
        var columnas = ctx.GetService<IDesignTimeModel>().Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties().Select(p => $"{e.GetTableName()}.{p.GetColumnName()}"))
            .ToList();

        Assert.All(columnas, c => Assert.Matches(SnakeCase(), c.Split('.')[1]));
        Assert.Contains("turnos_linea.fecha_operativa", columnas);
        Assert.Contains("lineas.tiempo_ciclo_estandar_seg", columnas);
        Assert.Contains("registros_paro.inicio_utc", columnas);
        Assert.Contains("ordenes_produccion.costo_unitario_mxn", columnas);
        Assert.Contains("usuarios.planta_ids", columnas);
    }

    [Fact]
    public void Turno_de_linea_es_unico_por_linea_fecha_y_turno()
    {
        using var ctx = CrearContexto();
        var entidad = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TurnoLinea))!;
        var indice = entidad.GetIndexes().Single(i => i.IsUnique);
        Assert.Equal(new[] { "LineaId", "FechaOperativa", "TurnoId" }, indice.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void Los_eventos_de_dominio_no_se_mapean()
    {
        using var ctx = CrearContexto();
        var model = ctx.GetService<IDesignTimeModel>().Model;
        Assert.All(model.GetEntityTypes(), e => Assert.Null(e.FindProperty("DomainEvents")));
    }

    [Fact]
    public void Genera_el_script_ddl_sin_errores()
    {
        using var ctx = CrearContexto();
        var sql = ctx.Database.GenerateCreateScript();

        Assert.Contains("CREATE TABLE turnos_linea", sql);
        Assert.Contains("ck_registros_produccion_piezas", sql);
        Assert.Contains("numeric(10,3)", sql);
        Assert.Contains("CREATE TABLE usuarios", sql);
        Assert.Contains("uuid[]", sql);
    }
}
