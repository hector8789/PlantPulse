using FluentValidation;
using MediatR;
using PlantPulse.Domain.Common;

namespace PlantPulse.Application.Common;

/// <summary>
/// Ejecuta los validadores de FluentValidation antes del handler.
/// Si hay errores lanza ValidationException, que la API convierte en 422 con ValidationProblemDetails.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var lista = validators as IValidator<TRequest>[] ?? validators.ToArray();
        if (lista.Length == 0) return await next();

        var contexto = new ValidationContext<TRequest>(request);
        var resultados = await Task.WhenAll(lista.Select(v => v.ValidateAsync(contexto, cancellationToken)));
        var errores = resultados.SelectMany(r => r.Errors).Where(e => e is not null).ToList();

        if (errores.Count != 0) throw new ValidationException(errores);
        return await next();
    }
}

/// <summary>Envuelve un evento de dominio para publicarlo con MediatR sin que el dominio dependa de MediatR.</summary>
public sealed record DomainEventNotification<TEvent>(TEvent Evento) : INotification where TEvent : IDomainEvent;
