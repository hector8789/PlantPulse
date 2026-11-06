using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantPulse.Application.PowerBi;
using PlantPulse.Infrastructure.Persistence;

namespace PlantPulse.Infrastructure.PowerBi;

/// <summary>
/// Refresh de Power BI sin proceso en segundo plano: el estado vive en <c>powerbi_refresh_estado</c> y se
/// atiende al cerrar un turno o al pedir un embed token. La fila se bloquea con <c>FOR UPDATE</c>, así
/// dos instancias de Lambda nunca lanzan el mismo refresh. Nunca lanza excepciones al que llama.
/// </summary>
internal sealed class PowerBiRefreshCoordinator(
    PlantPulseDbContext db,
    PowerBiSettings settings,
    IPowerBiDatasetRefresher refresher,
    TimeProvider reloj,
    ILogger<PowerBiRefreshCoordinator> logger) : IPowerBiRefreshScheduler
{
    private readonly PoliticaRefresh _politica = new(
        TimeSpan.FromSeconds(settings.VentanaAgrupacionSegundos),
        TimeSpan.FromMinutes(settings.IntervaloMinimoMinutos),
        settings.MaximoRefreshPorDia);

    public Task SolicitarAsync(string motivo, CancellationToken ct)
    {
        if (!Activo()) return Task.CompletedTask;
        logger.LogInformation("Refresh de Power BI solicitado: {Motivo}", motivo);
        return ProcesarAsync(solicitar: true, ct);
    }

    public Task IntentarPendienteAsync(CancellationToken ct) =>
        Activo() ? ProcesarAsync(solicitar: false, ct) : Task.CompletedTask;

    private bool Activo()
    {
        if (!settings.RefreshHabilitado) return false;
        if (settings.EstaConfigurado) return true;
        logger.LogDebug("Power BI no está configurado; se ignora el refresh.");
        return false;
    }

    private async Task ProcesarAsync(bool solicitar, CancellationToken ct)
    {
        bool lanzar;
        try
        {
            lanzar = await ConBloqueoAsync(estado =>
            {
                var ahora = reloj.GetUtcNow();
                if (solicitar) estado.Solicitar(ahora);
                if (!_politica.PuedeRefrescar(estado.Instantanea(), ahora)) return false;
                estado.RegistrarRefresh(ahora);
                return true;
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo leer o guardar el estado del refresh de Power BI.");
            return;
        }

        if (!lanzar) return;

        try
        {
            await refresher.RefrescarAsync(ct);
            logger.LogInformation("Refresh de Power BI lanzado (máximo {Maximo} al día).", _politica.MaximoPorDia);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falló el refresh de Power BI; queda pendiente para el siguiente intento.");
            try
            {
                await ConBloqueoAsync(estado => { estado.Reintentar(reloj.GetUtcNow()); return true; }, CancellationToken.None);
            }
            catch (Exception ex2)
            {
                logger.LogError(ex2, "No se pudo marcar el refresh de Power BI como pendiente.");
            }
        }
    }

    /// <summary>Lee la fila con FOR UPDATE, aplica el cambio, guarda y la suelta del change tracker.</summary>
    private async Task<bool> ConBloqueoAsync(Func<EstadoRefreshPowerBi, bool> accion, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var estado = (await db.Set<EstadoRefreshPowerBi>()
                .FromSql($"SELECT * FROM powerbi_refresh_estado WHERE id = {EstadoRefreshPowerBi.IdUnico} FOR UPDATE")
                .ToListAsync(ct))
            .SingleOrDefault()
            ?? throw new InvalidOperationException("Falta la fila de powerbi_refresh_estado: aplica las migraciones.");
        try
        {
            var resultado = accion(estado);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return resultado;
        }
        finally
        {
            // Sin esto, la siguiente lectura devolvería esta instancia en caché en vez de la fila actual.
            db.Entry(estado).State = EntityState.Detached;
        }
    }
}
