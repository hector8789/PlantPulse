using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantPulse.Application.Common;
using PlantPulse.Domain.Common;

namespace PlantPulse.Infrastructure.Persistence;

/// <summary>
/// Guarda los cambios y después publica los eventos de dominio de los agregados modificados.
/// Publicar después del commit evita, por ejemplo, refrescar Power BI por un cierre que no se guardó.
/// </summary>
internal sealed class UnitOfWork(PlantPulseDbContext db, IPublisher publisher) : IUnitOfWork
{
    private const string UniqueViolation = "23505";

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var agregados = db.ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();
        var eventos = agregados.SelectMany(a => a.DomainEvents).ToList();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation } pg)
        {
            throw new ConflictException("conflicto.duplicado",
                $"El registro ya existe ({pg.ConstraintName}). Recarga los datos e intenta de nuevo.");
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("conflicto.concurrencia",
                "Otro usuario modificó estos datos al mismo tiempo. Recarga e intenta de nuevo.");
        }

        foreach (var a in agregados) a.ClearDomainEvents();

        foreach (var evento in eventos)
        {
            var tipo = typeof(DomainEventNotification<>).MakeGenericType(evento.GetType());
            var notificacion = Activator.CreateInstance(tipo, evento)!;
            await publisher.Publish(notificacion, cancellationToken);
        }
    }
}
