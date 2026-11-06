using PlantPulse.Domain.Common;

namespace PlantPulse.Domain.CausasParo;

public enum TipoParo
{
    /// <summary>Comida, mantenimiento programado: se descuenta del tiempo planificado.</summary>
    Planificado = 1,

    /// <summary>Falla, falta de material, setup: se descuenta de la disponibilidad.</summary>
    NoPlanificado = 2,
}

/// <summary>Agrupador para el Pareto de causas de paro.</summary>
public enum CategoriaParo
{
    Programado = 1,
    Mecanica = 2,
    Electrica = 3,
    Material = 4,
    Calidad = 5,
    CambioModelo = 6,
    Operacion = 7,
}

/// <summary>Catálogo de causas de paro.</summary>
public sealed class CausaParo : AggregateRoot
{
    public static class Errores
    {
        public const string DatoInvalido = "causa_paro.dato_invalido";
    }

    public string Codigo { get; private set; } = null!;
    public string Descripcion { get; private set; } = null!;
    public TipoParo Tipo { get; private set; }
    public CategoriaParo Categoria { get; private set; }
    public bool Activa { get; private set; }

    public bool EsPlanificado => Tipo == TipoParo.Planificado;

    private CausaParo() { }

    private CausaParo(Guid id, string codigo, string descripcion, TipoParo tipo, CategoriaParo categoria) : base(id)
    {
        Codigo = codigo;
        Descripcion = descripcion;
        Tipo = tipo;
        Categoria = categoria;
        Activa = true;
    }

    public static CausaParo Crear(string codigo, string descripcion, TipoParo tipo, CategoriaParo categoria)
    {
        if (!Enum.IsDefined(tipo))
            throw new DomainException(Errores.DatoInvalido, "Tipo de paro inválido.");
        if (!Enum.IsDefined(categoria))
            throw new DomainException(Errores.DatoInvalido, "Categoría de paro inválida.");

        return new CausaParo(
            NuevoId(),
            Guard.Texto(codigo, 20, Errores.DatoInvalido, "El código de causa").ToUpperInvariant(),
            Guard.Texto(descripcion, 120, Errores.DatoInvalido, "La descripción de la causa"),
            tipo,
            categoria);
    }

    public void Desactivar() => Activa = false;

    public void Activar() => Activa = true;
}
