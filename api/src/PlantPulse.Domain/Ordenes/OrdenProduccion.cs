using PlantPulse.Domain.Common;
using PlantPulse.Domain.Lineas;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Domain.Ordenes;

public enum EstadoOrden
{
    Liberada = 1,
    EnProceso = 2,
    Terminada = 3,
}

/// <summary>
/// Orden de producción. En el Día 3 llegará del ERP simulado (OData); el número es el del ERP.
/// El cumplimiento del plan se calcula comparando <see cref="CantidadPlaneada"/> contra las piezas buenas registradas.
/// </summary>
public sealed class OrdenProduccion : AggregateRoot
{
    public static class Errores
    {
        public const string DatoInvalido = "orden.dato_invalido";
        public const string TransicionInvalida = "orden.transicion_invalida";
        public const string LineaDistinta = "orden.linea_distinta";
    }

    public string Numero { get; private set; } = null!;
    public Guid PlantaId { get; private set; }
    public Guid LineaId { get; private set; }
    public string MaterialCodigo { get; private set; } = null!;
    public string MaterialDescripcion { get; private set; } = null!;
    public int CantidadPlaneada { get; private set; }
    public DateOnly FechaInicioPlan { get; private set; }
    public DateOnly FechaFinPlan { get; private set; }

    /// <summary>Si es null se usa el tiempo de ciclo estándar de la línea.</summary>
    public TiempoCiclo? TiempoCicloIdeal { get; private set; }

    /// <summary>Costo estándar por pieza en MXN (viene del ERP). Se usa para valuar el scrap.</summary>
    public decimal? CostoUnitarioMxn { get; private set; }

    public EstadoOrden Estado { get; private set; }

    public bool AceptaProduccion => Estado != EstadoOrden.Terminada;

    private OrdenProduccion() { }

    private OrdenProduccion(Guid id) : base(id) { }

    public static OrdenProduccion Crear(
        string numero,
        Linea linea,
        string materialCodigo,
        string materialDescripcion,
        int cantidadPlaneada,
        DateOnly fechaInicioPlan,
        DateOnly fechaFinPlan,
        TiempoCiclo? tiempoCicloIdeal = null,
        decimal? costoUnitarioMxn = null)
    {
        ArgumentNullException.ThrowIfNull(linea);
        ValidarPlan(cantidadPlaneada, fechaInicioPlan, fechaFinPlan);
        ValidarCosto(costoUnitarioMxn);

        return new OrdenProduccion(NuevoId())
        {
            Numero = Guard.Texto(numero, 20, Errores.DatoInvalido, "El número de orden"),
            PlantaId = linea.PlantaId,
            LineaId = linea.Id,
            MaterialCodigo = Guard.Texto(materialCodigo, 40, Errores.DatoInvalido, "El código de material").ToUpperInvariant(),
            MaterialDescripcion = Guard.Texto(materialDescripcion, 120, Errores.DatoInvalido, "La descripción del material"),
            CantidadPlaneada = cantidadPlaneada,
            FechaInicioPlan = fechaInicioPlan,
            FechaFinPlan = fechaFinPlan,
            TiempoCicloIdeal = tiempoCicloIdeal,
            CostoUnitarioMxn = costoUnitarioMxn,
            Estado = EstadoOrden.Liberada,
        };
    }

    /// <summary>Tiempo de ciclo a usar: el de la orden si existe; si no, el estándar de la línea.</summary>
    public TiempoCiclo ResolverTiempoCiclo(Linea linea)
    {
        ArgumentNullException.ThrowIfNull(linea);
        if (linea.Id != LineaId)
            throw new DomainException(Errores.LineaDistinta, $"La orden {Numero} no pertenece a la línea {linea.Codigo}.");
        return TiempoCicloIdeal ?? linea.TiempoCicloEstandar;
    }

    /// <summary>Liberada → EnProceso. Idempotente si ya está en proceso.</summary>
    public void Iniciar()
    {
        if (Estado == EstadoOrden.Terminada)
            throw new DomainException(Errores.TransicionInvalida, $"La orden {Numero} ya está terminada.");
        Estado = EstadoOrden.EnProceso;
    }

    /// <summary>Liberada o EnProceso → Terminada. Después ya no acepta producción.</summary>
    public void Terminar()
    {
        if (Estado == EstadoOrden.Terminada)
            throw new DomainException(Errores.TransicionInvalida, $"La orden {Numero} ya está terminada.");
        Estado = EstadoOrden.Terminada;
    }

    /// <summary>
    /// Actualiza el plan con lo que reporta el ERP. Devuelve true si algo cambió.
    /// Una orden terminada ya no se modifica.
    /// </summary>
    public bool ActualizarPlan(
        int cantidadPlaneada, DateOnly fechaInicioPlan, DateOnly fechaFinPlan, TiempoCiclo? tiempoCicloIdeal, decimal? costoUnitarioMxn)
    {
        if (Estado == EstadoOrden.Terminada)
            throw new DomainException(Errores.TransicionInvalida, $"La orden {Numero} está terminada y no se puede modificar.");
        ValidarPlan(cantidadPlaneada, fechaInicioPlan, fechaFinPlan);
        ValidarCosto(costoUnitarioMxn);

        var cambio = CantidadPlaneada != cantidadPlaneada
            || FechaInicioPlan != fechaInicioPlan
            || FechaFinPlan != fechaFinPlan
            || TiempoCicloIdeal != tiempoCicloIdeal
            || CostoUnitarioMxn != costoUnitarioMxn;

        CantidadPlaneada = cantidadPlaneada;
        FechaInicioPlan = fechaInicioPlan;
        FechaFinPlan = fechaFinPlan;
        TiempoCicloIdeal = tiempoCicloIdeal;
        CostoUnitarioMxn = costoUnitarioMxn;
        return cambio;
    }

    /// <summary>Asigna el costo estándar (permitido aun terminada: solo afecta la valuación del scrap).</summary>
    public void AsignarCostoEstandar(decimal costoUnitarioMxn)
    {
        ValidarCosto(costoUnitarioMxn);
        CostoUnitarioMxn = costoUnitarioMxn;
    }

    private static void ValidarPlan(int cantidadPlaneada, DateOnly inicio, DateOnly fin)
    {
        if (cantidadPlaneada <= 0)
            throw new DomainException(Errores.DatoInvalido, "La cantidad planeada debe ser mayor a 0.");
        if (fin < inicio)
            throw new DomainException(Errores.DatoInvalido, "La fecha fin del plan no puede ser anterior a la de inicio.");
    }

    private static void ValidarCosto(decimal? costo)
    {
        if (costo is < 0m)
            throw new DomainException(Errores.DatoInvalido, "El costo unitario no puede ser negativo.");
    }
}
