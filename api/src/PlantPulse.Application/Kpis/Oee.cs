using FluentValidation;
using MediatR;
using PlantPulse.Application.Common;
using PlantPulse.Application.Turnos;
using PlantPulse.Domain.Produccion;
using PlantPulse.Domain.ValueObjects;

namespace PlantPulse.Application.Kpis;

public sealed record OeeLineaTurnoDto(Guid LineaId, string LineaCodigo, int TurnoNumero, int Turnos, OeeDto Oee);

public sealed record OeeLineaDto(Guid LineaId, string LineaCodigo, int Turnos, OeeDto Oee);

public sealed record OeeResumenDto(
    DateOnly Desde,
    DateOnly Hasta,
    int Turnos,
    OeeDto Total,
    IReadOnlyList<OeeLineaDto> PorLinea,
    IReadOnlyList<OeeLineaTurnoDto> PorLineaTurno);

/// <summary>
/// OEE agregado por línea y por línea + turno. No promedia porcentajes: suma tiempos y piezas
/// y recalcula, así un turno corto no pesa lo mismo que uno completo.
/// </summary>
public sealed record ObtenerOeeQuery(Guid? PlantaId, Guid? LineaId, DateOnly Desde, DateOnly Hasta) : IRequest<OeeResumenDto>;

public sealed class ObtenerOeeValidator : AbstractValidator<ObtenerOeeQuery>
{
    public const int MaximoDias = 92;

    public ObtenerOeeValidator()
    {
        RuleFor(x => x.Desde).NotEmpty().WithMessage("La fecha inicial es obligatoria.");
        RuleFor(x => x.Hasta).NotEmpty().WithMessage("La fecha final es obligatoria.")
            .GreaterThanOrEqualTo(x => x.Desde).WithMessage("La fecha final no puede ser anterior a la inicial.")
            .Must((q, hasta) => hasta.DayNumber - q.Desde.DayNumber < MaximoDias)
            .WithMessage($"El rango no puede exceder {MaximoDias} días.");
    }
}

public sealed class ObtenerOeeHandler(
    ITurnoLineaRepository turnos, ILineaRepository lineas, IPlantaRepository plantas, ICurrentUser usuario)
    : IRequestHandler<ObtenerOeeQuery, OeeResumenDto>
{
    public async Task<OeeResumenDto> Handle(ObtenerOeeQuery request, CancellationToken ct)
    {
        var plantasVisibles = usuario.PlantasVisibles(request.PlantaId);
        if (request.LineaId is { } lineaId)
        {
            var linea = await lineas.ObtenerAsync(lineaId, ct) ?? throw new NotFoundException("Línea", lineaId);
            usuario.AsegurarAccesoPlanta(linea.PlantaId);
        }

        var lista = await turnos.ListarAsync(plantasVisibles, request.LineaId, request.Desde, request.Hasta, ct);
        var lineasPorId = (await lineas.ListarAsync(plantasVisibles, ct)).ToDictionary(l => l.Id);
        var numeroTurno = (await plantas.ListarAsync(plantasVisibles, ct)).SelectMany(p => p.Turnos).ToDictionary(t => t.Id, t => t.Numero);
        string Codigo(Guid id) => lineasPorId.TryGetValue(id, out var l) ? l.Codigo : "?";

        var porLinea = lista.GroupBy(t => t.LineaId)
            .Select(g => new OeeLineaDto(g.Key, Codigo(g.Key), g.Count(), OeeDto.Desde(Agregar(g))))
            .OrderBy(x => x.LineaCodigo)
            .ToList();

        var porLineaTurno = lista.GroupBy(t => (t.LineaId, Numero: numeroTurno.GetValueOrDefault(t.TurnoId)))
            .Select(g => new OeeLineaTurnoDto(g.Key.LineaId, Codigo(g.Key.LineaId), g.Key.Numero, g.Count(), OeeDto.Desde(Agregar(g))))
            .OrderBy(x => x.LineaCodigo).ThenBy(x => x.TurnoNumero)
            .ToList();

        return new OeeResumenDto(request.Desde, request.Hasta, lista.Count, OeeDto.Desde(Agregar(lista)), porLinea, porLineaTurno);
    }

    /// <summary>Suma los componentes de varios turnos y recalcula el OEE con las mismas reglas del dominio.</summary>
    public static ResultadoOee Agregar(IEnumerable<TurnoLinea> turnos)
    {
        var resultados = turnos.Select(t => t.CalcularOee()).ToList();
        return ResultadoOee.Calcular(
            Duracion.Suma(resultados.Select(r => r.DuracionTurno)),
            Duracion.Suma(resultados.Select(r => r.ParosPlanificados)),
            Duracion.Suma(resultados.Select(r => r.ParosNoPlanificados)),
            resultados.Sum(r => r.SegundosIdeales),
            resultados.Aggregate(ConteoPiezas.Vacio, (acc, r) => acc + r.Piezas));
    }
}
