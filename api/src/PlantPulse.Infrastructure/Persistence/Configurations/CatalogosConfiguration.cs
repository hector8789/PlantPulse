using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantPulse.Domain.CausasParo;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.Ordenes;
using PlantPulse.Domain.Plantas;

namespace PlantPulse.Infrastructure.Persistence.Configurations;

internal sealed class LineaConfiguration : IEntityTypeConfiguration<Linea>
{
    public void Configure(EntityTypeBuilder<Linea> builder)
    {
        builder.ToTable("lineas");
        builder.ComoAgregado();

        builder.Property(l => l.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Nombre).HasMaxLength(120).IsRequired();
        builder.Property(l => l.TiempoCicloEstandar)
            .HasConversion(Conversores.TiempoCicloASegundos)
            .HasPrecision(10, 3)
            .HasColumnName("tiempo_ciclo_estandar_seg")
            .IsRequired();

        builder.HasOne<Planta>().WithMany().HasForeignKey(l => l.PlantaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.PlantaId, l.Codigo }).IsUnique();
    }
}

internal sealed class CausaParoConfiguration : IEntityTypeConfiguration<CausaParo>
{
    public void Configure(EntityTypeBuilder<CausaParo> builder)
    {
        builder.ToTable("causas_paro");
        builder.ComoAgregado();

        builder.Property(c => c.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Descripcion).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Tipo).ComoTexto();
        builder.Property(c => c.Categoria).ComoTexto();
        builder.Ignore(c => c.EsPlanificado);

        builder.HasIndex(c => c.Codigo).IsUnique();
    }
}

internal sealed class OrdenProduccionConfiguration : IEntityTypeConfiguration<OrdenProduccion>
{
    public void Configure(EntityTypeBuilder<OrdenProduccion> builder)
    {
        builder.ToTable("ordenes_produccion");
        builder.ComoAgregado();

        builder.Property(o => o.Numero).HasMaxLength(20).IsRequired();
        builder.Property(o => o.MaterialCodigo).HasMaxLength(40).IsRequired();
        builder.Property(o => o.MaterialDescripcion).HasMaxLength(120).IsRequired();
        builder.Property(o => o.TiempoCicloIdeal)
            .HasConversion(Conversores.TiempoCicloOpcionalASegundos)
            .HasPrecision(10, 3)
            .HasColumnName("tiempo_ciclo_ideal_seg")
            .IsRequired(false);
        builder.Property(o => o.CostoUnitarioMxn).HasPrecision(12, 4);
        builder.Property(o => o.Estado).ComoTexto();
        builder.Ignore(o => o.AceptaProduccion);

        builder.HasOne<Planta>().WithMany().HasForeignKey(o => o.PlantaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Linea>().WithMany().HasForeignKey(o => o.LineaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.Numero).IsUnique();
        builder.HasIndex(o => new { o.LineaId, o.FechaInicioPlan });
    }
}
