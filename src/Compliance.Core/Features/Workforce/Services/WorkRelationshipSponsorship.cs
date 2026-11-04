using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;

namespace Bdgrz.Compliance.Features.Workforce.Services;

/// <summary>Computes unsponsored work from current sponsor eligibility; never changes workforce facts.</summary>
public sealed class WorkRelationshipSponsorship(IWorkforceObservationDirectoryReader roster,
    WorkforceObservationReadConsistency consistency)
{
    public ValueTask<Result<IReadOnlyList<WorkRelationshipView>>> EvaluateAsync(Uuid tenantId,
        IReadOnlyList<WorkRelationshipView> relationships, CancellationToken ct) =>
        ValueTask.FromResult(Result<IReadOnlyList<WorkRelationshipView>>.Failure(
            new RequestError(RequestErrorKind.Conflict,
                $"Sponsorship evaluation from {roster.GetType().Name} and {consistency.GetType().Name} is not implemented.")));
}
