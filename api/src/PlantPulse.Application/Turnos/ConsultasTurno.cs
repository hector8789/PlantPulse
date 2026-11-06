using FluentValidation;
using MediatR;
using PlantPulse.Application.Common;

namespace PlantPulse.Application.Turnos;

public sealed record ObtenerTurnoQuery(Guid TurnoLineaId) : IRequest<TurnoLineaDto>;

public sealed class ObtenerTurnoHandler(ITurnoLineaRepository turnos, ICurrentUser usuario, TurnoLineaDtoFactory dtos)
    : IRequestHandler<ObtenerTurnoQuery, TurnoLineaDto>
{
    public async Task<TurnoLineaDto> Handle(ObtenerTurnoQuery request, CancellationToken ct) =>
        await dtos.CrearAsync(await TurnoAcceso.CargarAsync(turnos, usuario, request.TurnoLineaId, ct), ct);
}

/// <summary>Turnos de línea en un rango de fechas operativas (máximo 62 días).</summary>
public sealed record ListarTurnosQuery(Guid? PlantaId, Guid? LineaId, DateOnly Desde, DateOnly Hasta)
    : IRequest<IReadOnlyList<TurnoResumenDto>>;

public sealed class ListarTurnosValidator : AbstractValidator<ListarTurnosQuery>
{
    public const int MaximoDias = 62;

    public ListarTurnosValidator()
    {
        RuleFor(x => x.Desde).NotEmpty().WithMessage("La fecha inicial es obligatoria.");
        RuleFor(x => x.Hasta).NotEmpty().WithMessage("La fecha final es obligatoria.")
            .GreaterThanOrEqualTo(x => x.Desde).WithMessage("La fecha final no puede ser anterior a la inicial.")
            .Must((q, hasta) => hasta.DayNumber - q.Desde.DayNumber < MaximoDias)
            .WithMessage($"El rango no puede exceder {MaximoDias} días.");
    }
}

public sealed class ListarTurnosHandler(
    ITurnoLineaRepository turnos, ILineaRepository lineas, IPlantaRepository plantas, ICurrentUser usuario)
    : IRequestHandler<ListarTurnosQuery, IReadOnlyList<TurnoResumenDto>>
{
    public async Task<IReadOnlyList<TurnoResumenDto>> Handle(ListarTurnosQuery request, CancellationToken ct)
    {
        var plantasVisibles = usuario.PlantasVisibles(request.PlantaId);
        if (request.LineaId is { } lineaId)
        {
            var linea = await lineas.ObtenerAsync(lineaId, ct) ?? throw new NotFoundException("Línea", lineaId);
            usuario.AsegurarAccesoPlanta(linea.PlantaId);
        }

        var lista = await turnos.ListarAsync(plantasVisibles, request.LineaId, request.Desde, request.Hasta, ct);
        var lineasPorId = (await lineas.ListarAsync(plantasVisibles, ct)).ToDictionary(l => l.Id);
        var turnosPorId = (await plantas.ListarAsync(plantasVisibles, ct)).SelectMany(p => p.Turnos).ToDictionary(t => t.Id);

        return lista
            .OrderBy(t => t.FechaOperativa).ThenBy(t => t.InicioUtc).ThenBy(t => lineasPorId.GetValueOrDefault(t.LineaId)?.Codigo)
            .Select(t => new TurnoResumenDto(
                t.Id, t.PlantaId, t.LineaId, lineasPorId.GetValueOrDefault(t.LineaId)?.Codigo ?? "?",
                t.TurnoId, turnosPorId.GetValueOrDefault(t.TurnoId)?.Numero ?? 0,
                t.FechaOperativa, t.Estado.ToString(), t.InicioUtc, t.FinUtc, t.Paros.Count, OeeDto.Desde(t.CalcularOee())))
            .ToList();
    }
}
