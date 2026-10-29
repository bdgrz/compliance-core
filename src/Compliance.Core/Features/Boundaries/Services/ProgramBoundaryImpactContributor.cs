using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
namespace Bdgrz.Compliance.Features.Boundaries.Services;

public sealed class ProgramBoundaryImpactContributor : IBoundaryImpactContributor
{
    public string Context => "programs";

    public ValueTask<Result<BoundaryImpactContribution>> ContributeAsync(BoundaryView boundary,
        IReadOnlyList<BoundaryChange> changes, CancellationToken ct)
    {
        IReadOnlyList<BoundaryAffectedRecord> records =
            boundary.LatestApprovedVersion is not null && changes.Count > 0
                ? [new BoundaryAffectedRecord(boundary.TenantId, Context,
                    "program", boundary.ProgramId,
                    "The approved scope used by this program would change.")]
                : [];
        return ValueTask.FromResult(Result<BoundaryImpactContribution>.Success(
            new BoundaryImpactContribution(Context, records, true)));
    }
}
