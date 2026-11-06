using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.Produccion;

namespace PlantPulse.Infrastructure.Persistence.Configurations;

internal sealed class TurnoLineaConfiguration : IEntityTypeConfiguration<TurnoLinea>
{
    public void Configure(EntityTypeBuilder<TurnoLinea> builder)
    {
        builder.ToTable("turnos_linea");
        builder.ComoAgregado();

        builder.Property(t => t.Estado).ComoTexto();
        builder.Ignore(t => t.EstaCerrado);
        builder.Ignore(t => t.DuracionTurno);

        builder.HasOne<Planta>().WithMany().HasForeignKey(t => t.PlantaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Linea>().WithMany().HasForeignKey(t => t.LineaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Turno>().WithMany().HasForeignKey(t => t.TurnoId).OnDelete(DeleteBehavior.Restrict);

        // Un solo turno por línea, fecha operativa y turno de la planta.
        builder.HasIndex(t => new { t.LineaId, t.FechaOperativa, t.TurnoId }).IsUnique();
        // Consultas y RLS por planta y rango de fechas.
        builder.HasIndex(t => new { t.PlantaId, t.FechaOperativa });

        builder.HasMany(t => t.Registros)
            .WithOne()
            .HasForeignKey(r => r.TurnoLineaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Registros).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(t => t.Paros)
            .WithOne()
            .HasForeignKey(p => p.TurnoLineaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Paros).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RegistroProduccionConfiguration : IEntityTypeConfiguration<RegistroProduccion>
{
    public void Configure(EntityTypeBuilder<RegistroProduccion> builder)
    {
        builder.ToTable("registros_produccion", t =>
        {
            t.HasCheckConstraint("ck_registros_produccion_piezas", "piezas_buenas >= 0 AND piezas_buenas <= piezas_totales");
        });
        builder.ConClaveDeDominio();

        builder.Property(r => r.TiempoCicloIdeal)
            .HasConversion(Conversores.TiempoCicloASegundos)
            .HasPrecision(10, 3)
            .HasColumnName("tiempo_ciclo_ideal_seg")
            .IsRequired();
        builder.Ignore(r => r.Conteo);
        builder.Ignore(r => r.Scrap);
        builder.Ignore(r => r.SegundosIdeales);

        builder.HasOne<OrdenProduccion>().WithMany().HasForeignKey(r => r.OrdenProduccionId).OnDelete(DeleteBehavior.Restrict);

        // A lo más un registro por orden en cada turno.
        builder.HasIndex(r => new { r.TurnoLineaId, r.OrdenProduccionId }).IsUnique();
    }
}

internal sealed class RegistroParoConfiguration : IEntityTypeConfiguration<RegistroParo>
{
    public void Configure(EntityTypeBuilder<RegistroParo> builder)
    {
        builder.ToTable("registros_paro", t =>
        {
            t.HasCheckConstraint("ck_registros_paro_rango", "fin_utc > inicio_utc");
        });
        builder.ConClaveDeDominio();

        builder.Property(p => p.Tipo).ComoTexto();
        builder.Property(p => p.Comentario).HasMaxLength(TurnoLinea.MaximoComentario);
        builder.Ignore(p => p.Duracion);
        builder.Ignore(p => p.EsPlanificado);

        builder.HasOne<CausaParo>().WithMany().HasForeignKey(p => p.CausaParoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.TurnoLineaId, p.InicioUtc });
        builder.HasIndex(p => p.CausaParoId);
    }
}
