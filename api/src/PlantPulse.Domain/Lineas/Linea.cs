using PlantPulse.Domain.Common;
using PlantPulse.Domain.Plantas;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Domain.Lineas;

/// <summary>
/// Línea de producción de una planta. Tiene un tiempo de ciclo ideal estándar que la orden
/// de producción puede sobrescribir (como el valor por material de la hoja de ruta en SAP).
/// </summary>
public sealed class Linea : AggregateRoot
{
    public static class Errores
    {
        public const string DatoInvalido = "linea.dato_invalido";
        public const string PlantaInactiva = "linea.planta_inactiva";
    }

    public Guid PlantaId { get; private set; }
    public string Codigo { get; private set; } = null!;
    public string Nombre { get; private set; } = null!;
    public TiempoCiclo TiempoCicloEstandar { get; private set; } = null!;
    public bool Activa { get; private set; }

    private Linea() { }

    private Linea(Guid id, Guid plantaId, string codigo, string nombre, TiempoCiclo tiempoCiclo) : base(id)
    {
        PlantaId = plantaId;
        Codigo = codigo;
        Nombre = nombre;
        TiempoCicloEstandar = tiempoCiclo;
        Activa = true;
    }

    public static Linea Crear(Planta planta, string codigo, string nombre, TiempoCiclo tiempoCicloEstandar)
    {
        ArgumentNullException.ThrowIfNull(planta);
        ArgumentNullException.ThrowIfNull(tiempoCicloEstandar);
        if (!planta.Activa)
            throw new DomainException(Errores.PlantaInactiva, $"La planta {planta.Codigo} está inactiva.");

        return new Linea(
            NuevoId(),
            planta.Id,
            Guard.Texto(codigo, 20, Errores.DatoInvalido, "El código de línea").ToUpperInvariant(),
            Guard.Texto(nombre, 120, Errores.DatoInvalido, "El nombre de línea"),
            tiempoCicloEstandar);
    }

    public void CambiarTiempoCicloEstandar(TiempoCiclo nuevo)
    {
        ArgumentNullException.ThrowIfNull(nuevo);
        TiempoCicloEstandar = nuevo;
    }

    public void Desactivar() => Activa = false;

    public void Activar() => Activa = true;
}
