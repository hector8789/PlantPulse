using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantPulse.Domain.TiposCambio;
using PlantPulse.Domain.Usuarios;

namespace PlantPulse.Infrastructure.Persistence.Configurations;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.ComoAgregado();

        builder.Property(u => u.Email).HasMaxLength(120).IsRequired();
        builder.Property(u => u.Nombre).HasMaxLength(120).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(u => u.Roles).IsRequired();       // text[] en PostgreSQL
        builder.Property(u => u.PlantaIds).IsRequired();   // uuid[] en PostgreSQL
        builder.Ignore(u => u.EsAdmin);

        builder.HasIndex(u => u.Email).IsUnique();
    }
}

internal sealed class TipoCambioConfiguration : IEntityTypeConfiguration<TipoCambio>
{
    public void Configure(EntityTypeBuilder<TipoCambio> builder)
    {
        builder.ToTable("tipos_cambio");
        builder.ComoAgregado();

        builder.Property(t => t.Serie).HasMaxLength(20).IsRequired();
        builder.Property(t => t.PesosPorDolar).HasPrecision(10, 4);
        builder.Property(t => t.Fuente).HasMaxLength(40).IsRequired();

        builder.HasIndex(t => new { t.Serie, t.Fecha }).IsUnique();
    }
}
