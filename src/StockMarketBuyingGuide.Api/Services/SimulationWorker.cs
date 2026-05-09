using System.Threading.Channels;

namespace StockMarketBuyingGuide.Api.Services;

public class SimulationWorker(
    ChannelReader<Guid> jobQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<SimulationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var jobId in jobQueue.ReadAllAsync(stoppingToken))
            {
                logger.LogInformation("Starting simulation job {JobId}", jobId);

                using var scope = scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<SimulationService>();

                try
                {
                    await svc.RunSimulationAsync(jobId, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled error in simulation job {JobId}", jobId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — host is stopping
        }
    }
}
