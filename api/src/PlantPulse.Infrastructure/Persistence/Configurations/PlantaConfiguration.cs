using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantPulse.Domain.Plantas;

namespace PlantPulse.Infrastructure.Persistence.Configurations;

internal sealed class PlantaConfiguration : IEntityTypeConfiguration<Planta>
{
    public void Configure(EntityTypeBuilder<Planta> builder)
    {
        builder.ToTable("plantas");
        builder.ComoAgregado();

        builder.Property(p => p.Codigo).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Nombre).HasMaxLength(120).IsRequired();
        builder.Property(p => p.ZonaHoraria).HasMaxLength(64).IsRequired();
        builder.HasIndex(p => p.Codigo).IsUnique();

        builder.HasMany(p => p.Turnos)
            .WithOne()
            .HasForeignKey(t => t.PlantaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Turnos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TurnoConfiguration : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> builder)
    {
        builder.ToTable("turnos");
        builder.ConClaveDeDominio();

        builder.Property(t => t.Nombre).HasMaxLength(40).IsRequired();
        builder.Property(t => t.HoraInicio).IsRequired();   // time without time zone
        builder.Property(t => t.HoraFin).IsRequired();
        builder.Ignore(t => t.Horario);

        builder.HasIndex(t => new { t.PlantaId, t.Numero }).IsUnique();
    }
}
