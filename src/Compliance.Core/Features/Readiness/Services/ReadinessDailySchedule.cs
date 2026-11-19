using Bdgrz.Compliance.Features.Readiness.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Services;

/// <summary>Idempotently declares the product's daily trigger through Portia's scheduler.</summary>
public sealed class ReadinessDailySchedule(IRequestScheduler scheduler)
{
    public ValueTask<string> EnsureAsync(Uuid tenantId, CancellationToken ct = default) =>
        scheduler.EnsureAsync(new RunDailyReadinessAssessments(tenantId), new RequestScheduleSpec("0 0 * * *"),
            new RequestRouteValues(Realm: tenantId.ToString()),
            RequestActor.CreateSystem(DailyReadinessAssessment.ProcessId, "bdgrz.system"), ct);
}
