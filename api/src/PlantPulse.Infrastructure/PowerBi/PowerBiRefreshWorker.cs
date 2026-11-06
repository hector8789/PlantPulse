using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlantPulse.Application.Common;
using PlantPulse.Application.PowerBi;

namespace PlantPulse.Infrastructure.PowerBi;

/// <summary>
/// Recibe solicitudes de refresh (cierres de turno) y lanza un solo refresh agrupado en segundo plano,
/// respetando la <see cref="PoliticaRefresh"/>. El cierre del turno nunca espera a Power BI.
/// </summary>
internal sealed class PowerBiRefreshWorker(
    PowerBiSettings settings,
    IPowerBiDatasetRefresher refresher,
    TimeProvider reloj,
    ILogger<PowerBiRefreshWorker> logger) : BackgroundService, IPowerBiRefreshScheduler
{
    private readonly Channel<string> _cola = Channel.CreateBounded<string>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private readonly PoliticaRefresh _politica = new(
        TimeSpan.FromSeconds(settings.VentanaAgrupacionSegundos),
        TimeSpan.FromMinutes(settings.IntervaloMinimoMinutos),
        settings.MaximoRefreshPorDia);

    public void Solicitar(string motivo)
    {
        if (!settings.RefreshHabilitado)
        {
            logger.LogDebug("Refresh de Power BI deshabilitado; se ignora: {Motivo}", motivo);
            return;
        }
        _cola.Writer.TryWrite(motivo);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await _cola.Reader.WaitToReadAsync(stoppingToken))
            {
                var pendientes = Drenar();
                var espera = _politica.CalcularEspera(reloj.GetUtcNow());
                logger.LogInformation("Refresh de Power BI en {Espera:hh\\:mm\\:ss} ({Cantidad} solicitudes agrupadas).", espera, pendientes);

                await Task.Delay(espera, reloj, stoppingToken);
                pendientes += Drenar();

                if (!settings.EstaConfigurado)
                {
                    logger.LogWarning("Power BI no está configurado; se descartan {Cantidad} solicitudes de refresh.", pendientes);
                    continue;
                }

                try
                {
                    await refresher.RefrescarAsync(stoppingToken);
                    _politica.Registrar(reloj.GetUtcNow());
                    logger.LogInformation("Refresh de Power BI lanzado ({Hoy}/{Maximo} hoy).",
                        _politica.RefreshesDelDia(reloj.GetUtcNow()), _politica.MaximoPorDia);
                }
                catch (ExternalServiceException ex)
                {
                    logger.LogError(ex, "Falló el refresh de Power BI: {Mensaje}", ex.Message);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error inesperado al refrescar Power BI.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // La API se está apagando.
        }
    }

    private int Drenar()
    {
        var n = 0;
        while (_cola.Reader.TryRead(out _)) n++;
        return n;
    }
}
