using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Services;

/// <summary>Authoritative Firm services admission and impairment for a named attest engagement.</summary>
public interface IExaminationAttestEngagementReader
{
    ValueTask<Result> RequireAcceptedOpenAttestAsync(Uuid tenantId, Uuid engagementId, CancellationToken ct);
    ValueTask<Result> RequireUnimpairedAsync(Uuid tenantId, Uuid engagementId,
        DateOnly periodStart, DateOnly periodEnd, CancellationToken ct);
}
