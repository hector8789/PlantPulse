using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantPulse.Application.PowerBi;

namespace PlantPulse.Infrastructure.PowerBi;

/// <summary>
/// Fila única (Id = 1) con el estado del refresh de Power BI. Vive en la base para que la política
/// se respete aunque la API corra en Lambda (sin proceso en segundo plano) o en varias instancias.
/// </summary>
internal sealed class EstadoRefreshPowerBi
{
    public const int IdUnico = 1;

    public int Id { get; private set; } = IdUnico;
    public bool Pendiente { get; private set; }
    public DateTimeOffset? SolicitadoEnUtc { get; private set; }
    public DateTimeOffset? UltimoRefreshUtc { get; private set; }
    public DateOnly? FechaConteo { get; private set; }
    public int ConteoDelDia { get; private set; }

    public EstadoRefresh Instantanea() =>
        new(Pendiente, SolicitadoEnUtc, UltimoRefreshUtc, FechaConteo, ConteoDelDia);

    /// <summary>Si ya había un pendiente, conserva la hora de la primera solicitud (agrupación).</summary>
    public void Solicitar(DateTimeOffset ahoraUtc)
    {
        if (Pendiente) return;
        Pendiente = true;
        SolicitadoEnUtc = ahoraUtc;
    }

    public void RegistrarRefresh(DateTimeOffset ahoraUtc)
    {
        var hoy = DateOnly.FromDateTime(ahoraUtc.UtcDateTime);
        ConteoDelDia = FechaConteo == hoy ? ConteoDelDia + 1 : 1;
        FechaConteo = hoy;
        UltimoRefreshUtc = ahoraUtc;
        Pendiente = false;
    }

    /// <summary>El refresh falló: vuelve a quedar pendiente. El conteo no se devuelve (conservador con el límite).</summary>
    public void Reintentar(DateTimeOffset ahoraUtc)
    {
        Pendiente = true;
        SolicitadoEnUtc = ahoraUtc;
    }
}

internal sealed class EstadoRefreshPowerBiConfiguration : IEntityTypeConfiguration<EstadoRefreshPowerBi>
{
    public void Configure(EntityTypeBuilder<EstadoRefreshPowerBi> builder)
    {
        builder.ToTable("powerbi_refresh_estado", t =>
            t.HasCheckConstraint("ck_powerbi_refresh_estado_fila_unica", $"id = {EstadoRefreshPowerBi.IdUnico}"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // La fila se crea con la migración: así nunca hay que insertarla en tiempo de ejecución.
        builder.HasData(new { Id = EstadoRefreshPowerBi.IdUnico, Pendiente = false, ConteoDelDia = 0 });
    }
}
