using Bdgrz.Compliance.Features.Work.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Features.Work;

sealed partial class WorkEscalationBackgroundService(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<WorkEscalationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<WorkEscalationSweep>()
                    .RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                LogSweepFailed(logger);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(EventId = 41512, Level = LogLevel.Warning,
        Message = "Overdue work escalation sweep failed; the next poll will re-read current source work.")]
    static partial void LogSweepFailed(ILogger logger);
}
