using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;

namespace Bdgrz.Compliance.Features.Workforce.Services;

/// <summary>Computes unsponsored work from current sponsor eligibility; never changes workforce facts.</summary>
public sealed class WorkRelationshipSponsorship(IWorkforceObservationDirectoryReader roster,
    WorkforceObservationReadConsistency consistency)
{
    public async ValueTask<Result<IReadOnlyList<WorkRelationshipView>>> EvaluateAsync(Uuid tenantId,
        IReadOnlyList<WorkRelationshipView> relationships, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(relationships);
        if (relationships.Any(relationship => relationship.TenantId != tenantId))
            return Result<IReadOnlyList<WorkRelationshipView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The work relationships were not found."));
        if (!relationships.Any(relationship => relationship.WorkerType == "external_collaborator"))
            return Result<IReadOnlyList<WorkRelationshipView>>.Success(
                [.. relationships.Select(relationship => relationship with { Unsponsored = false })]);
        var ready = await consistency.EnsureCaughtUpAsync(tenantId, ct).ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<IReadOnlyList<WorkRelationshipView>>.Failure(ready.Error);
        var evaluated = new List<WorkRelationshipView>(relationships.Count);
        foreach (var relationship in relationships)
        {
            var unsponsored = false;
            if (relationship.WorkerType == "external_collaborator")
            {
                if (relationship.SponsorPersonId is not { } sponsorId || sponsorId == Uuid.Empty)
                    unsponsored = true;
                else
                    unsponsored = !await roster.HasActiveInternalRelationshipAsync(tenantId, sponsorId, ct)
                        .ConfigureAwait(false);
            }
            evaluated.Add(relationship with { Unsponsored = unsponsored });
        }
        return Result<IReadOnlyList<WorkRelationshipView>>.Success(evaluated);
    }
}
