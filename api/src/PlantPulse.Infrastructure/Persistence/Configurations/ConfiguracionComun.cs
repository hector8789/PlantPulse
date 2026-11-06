using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PlantPulse.Domain.Common;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Infrastructure.Persistence.Configurations;

internal static class ConfiguracionComun
{
    /// <summary>
    /// Clave Guid asignada por el dominio (v7). ValueGeneratedNever es obligatorio: sin él, EF trata como
    /// existente (UPDATE) a un hijo nuevo con Id ya asignado que se agrega a un agregado cargado.
    /// </summary>
    public static EntityTypeBuilder<T> ConClaveDeDominio<T>(this EntityTypeBuilder<T> builder) where T : Entity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        return builder;
    }

    /// <summary>Raíz de agregado: clave de dominio y sin mapear la lista de eventos.</summary>
    public static EntityTypeBuilder<T> ComoAgregado<T>(this EntityTypeBuilder<T> builder) where T : AggregateRoot
    {
        builder.ConClaveDeDominio();
        builder.Ignore(e => e.DomainEvents);
        return builder;
    }

    /// <summary>Enums como texto ("NoPlanificado") para que la base y Power BI sean legibles.</summary>
    public static PropertyBuilder<TEnum> ComoTexto<TEnum>(this PropertyBuilder<TEnum> builder) where TEnum : struct, Enum =>
        builder.HasConversion<string>().HasMaxLength(20).IsRequired();
}

internal static class Conversores
{
    /// <summary>TiempoCiclo ⇄ segundos (numeric(10,3)). EF no invoca el conversor con null.</summary>
    public static readonly ValueConverter<TiempoCiclo, decimal> TiempoCicloASegundos =
        new(v => v.Segundos, v => TiempoCiclo.DeSegundos(v));

    /// <summary>Misma conversión para propiedades opcionales (TiempoCiclo?). La columna sigue siendo numeric(10,3) NULL.</summary>
    public static readonly ValueConverter<TiempoCiclo?, decimal> TiempoCicloOpcionalASegundos =
        new(v => v!.Segundos, v => TiempoCiclo.DeSegundos(v));
}
