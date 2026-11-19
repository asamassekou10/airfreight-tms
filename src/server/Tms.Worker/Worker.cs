namespace Tms.Worker;

public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TMS background worker started; no message consumers are configured yet.");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
