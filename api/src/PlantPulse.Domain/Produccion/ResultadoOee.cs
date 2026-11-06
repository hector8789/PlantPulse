using PlantPulse.Domain.Common;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Domain.Produccion;

/// <summary>
/// Desglose del OEE de un turno de línea.
/// <list type="bullet">
/// <item>Tiempo planificado = duración del turno − paros planificados.</item>
/// <item>Tiempo operativo = tiempo planificado − paros no planificados.</item>
/// <item>Disponibilidad = operativo / planificado (planificado = 0 → 0%).</item>
/// <item>Desempeño = Σ(ciclo ideal × piezas) / operativo, limitado a 100% (operativo = 0 → 0%).</item>
/// <item>Calidad = buenas / totales (totales = 0 → 0%).</item>
/// <item>OEE = Disponibilidad × Desempeño × Calidad.</item>
/// </list>
/// </summary>
public sealed record ResultadoOee
{
    public static class Errores
    {
        public const string TiemposInconsistentes = "oee.tiempos_inconsistentes";
    }

    public required Duracion DuracionTurno { get; init; }
    public required Duracion ParosPlanificados { get; init; }
    public required Duracion TiempoPlanificado { get; init; }
    public required Duracion ParosNoPlanificados { get; init; }
    public required Duracion TiempoOperativo { get; init; }

    /// <summary>Σ(ciclo ideal × piezas totales), en segundos.</summary>
    public required decimal SegundosIdeales { get; init; }

    public required ConteoPiezas Piezas { get; init; }

    public required Porcentaje Disponibilidad { get; init; }
    public required Porcentaje Desempeno { get; init; }
    public required Porcentaje Calidad { get; init; }
    public required Porcentaje Oee { get; init; }

    /// <summary>
    /// Desempeño sin limitar. Si pasa de 1 el ciclo ideal o el conteo están mal capturados.
    /// Con tiempo operativo 0 la razón no existe y vale 0 (ver <see cref="DesempenoExcedido"/>).
    /// </summary>
    public required decimal DesempenoCalculado { get; init; }

    /// <summary>
    /// True cuando el desempeño superó 100% y se limitó, o cuando hay piezas sin tiempo operativo.
    /// Application lo registra como advertencia.
    /// </summary>
    public required bool DesempenoExcedido { get; init; }

    public int Scrap => Piezas.Scrap;

    public static ResultadoOee Calcular(
        Duracion duracionTurno,
        Duracion parosPlanificados,
        Duracion parosNoPlanificados,
        decimal segundosIdeales,
        ConteoPiezas piezas)
    {
        ArgumentNullException.ThrowIfNull(piezas);
        if (segundosIdeales < 0m)
            throw new DomainException(Errores.TiemposInconsistentes, "El tiempo ideal no puede ser negativo.");
        if (parosPlanificados > duracionTurno)
            throw new DomainException(Errores.TiemposInconsistentes, "Los paros planificados exceden la duración del turno.");

        var planificado = duracionTurno - parosPlanificados;
        if (parosNoPlanificados > planificado)
            throw new DomainException(Errores.TiemposInconsistentes, "Los paros no planificados exceden el tiempo planificado.");

        var operativo = planificado - parosNoPlanificados;

        var disponibilidad = Porcentaje.DeRazon(operativo.TotalSegundos, planificado.TotalSegundos, Porcentaje.Cero);

        // Sin tiempo operativo el desempeño es 0. Si aun así hay piezas, se marca como excedido.
        var desempeno = Porcentaje.DeRazonLimitada(
            segundosIdeales, operativo.TotalSegundos, Porcentaje.Cero, out var desempenoCalculado);
        var excedido = operativo.TotalSegundos == 0m ? segundosIdeales > 0m : desempenoCalculado > 1m;

        var calidad = piezas.Calidad;

        return new ResultadoOee
        {
            DuracionTurno = duracionTurno,
            ParosPlanificados = parosPlanificados,
            TiempoPlanificado = planificado,
            ParosNoPlanificados = parosNoPlanificados,
            TiempoOperativo = operativo,
            SegundosIdeales = segundosIdeales,
            Piezas = piezas,
            Disponibilidad = disponibilidad,
            Desempeno = desempeno,
            Calidad = calidad,
            Oee = disponibilidad.Por(desempeno).Por(calidad),
            DesempenoCalculado = desempenoCalculado,
            DesempenoExcedido = excedido,
        };
    }
}
