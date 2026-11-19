using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Resumes initial criteria seed creation using deterministic, individually owned operations.</summary>
/// <remarks>Register only in a worker host after its source adapters and criteria participants are enabled.</remarks>
public sealed class PlatformCriteriaSeedWorker(IServiceScopeFactory scopes, ILogger<PlatformCriteriaSeedWorker> logger)
    : BackgroundService
{
    static readonly Action<ILogger, Exception?> RetryFailed = LoggerMessage.Define(LogLevel.Warning,
        new EventId(1, nameof(RetryFailed)), "Initial criteria seed could not progress and will retry.");
    static readonly Action<ILogger, Exception?> ConflictingSeed = LoggerMessage.Define(LogLevel.Error,
        new EventId(2, nameof(ConflictingSeed)), "Initial criteria seed has conflicting source content and requires intervention.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await RunOnceAsync(stoppingToken).ConfigureAwait(false);
                if (result.Status == CriteriaSeedWriteStatus.Confirmed)
                    return;
                if (result.Status == CriteriaSeedWriteStatus.Conflict)
                {
                    ConflictingSeed(logger, null);
                    return;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                RetryFailed(logger, exception);
            }
            // Participant signals are driven independently; avoid repeatedly
            // rescanning the fixed release while waiting for their next sweep.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }

    internal async ValueTask<CriteriaSeedBootstrapResult> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var writer = new PlatformCriteriaSeedWriter(new RequestDispatchContext(RequestActor.System),
            services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<IAggregateExecutor>(),
            services.GetRequiredService<ICriteriaExactSourceReader>(), services.GetService<ICriteriaFreshEditionHead>(),
            services.GetService<ICriteriaSourceCutCoverage>(), services.GetService<ICriteriaPublicationAuthority>(),
            services.GetService<ICriteriaParticipantCausalOrder>(), services.GetService<ICriteriaFreshEntryHead>(),
            services.GetService<ICriteriaFreshLabelClaimHead>());
        return await new PlatformCriteriaSeedBootstrap(writer).RunAsync(ct).ConfigureAwait(false);
    }
}
