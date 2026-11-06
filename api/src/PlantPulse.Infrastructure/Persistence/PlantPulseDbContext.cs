using Microsoft.EntityFrameworkCore;
using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.Produccion;
using PlantPulse.Domain.TiposCambio;
using PlantPulse.Domain.Usuarios;

namespace PlantPulse.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core sobre PostgreSQL. Solo expone raíces de agregado: Turno, RegistroProduccion
/// y RegistroParo se cargan y se modifican a través de su agregado.
/// Los nombres de tablas y columnas salen en snake_case (EFCore.NamingConventions).
/// </summary>
public sealed class PlantPulseDbContext(DbContextOptions<PlantPulseDbContext> options) : DbContext(options)
{
    public DbSet<Planta> Plantas => Set<Planta>();
    public DbSet<Linea> Lineas => Set<Linea>();
    public DbSet<CausaParo> CausasParo => Set<CausaParo>();
    public DbSet<OrdenProduccion> OrdenesProduccion => Set<OrdenProduccion>();
    public DbSet<TurnoLinea> TurnosLinea => Set<TurnoLinea>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<TipoCambio> TiposCambio => Set<TipoCambio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlantPulseDbContext).Assembly);
    }
}
