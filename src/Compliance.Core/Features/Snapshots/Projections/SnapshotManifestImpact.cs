using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Snapshots.Models;
namespace Bdgrz.Compliance.Features.Snapshots.Projections;

static class SnapshotManifestImpact
{
    public static Result<IReadOnlyList<string>> ChangedSources(ProgramScopeManifest predecessor,
        ProgramScopeManifest amendment)
    {
        if (predecessor.FormatVersion != 1 || amendment.FormatVersion != 1 ||
            predecessor.TenantId != amendment.TenantId || predecessor.ProgramId != amendment.ProgramId)
            return Result<IReadOnlyList<string>>.Failure(new RequestError(RequestErrorKind.Validation,
                "Snapshot impact compares v1 manifests of one tenant program only."));

        var changed = new List<string>(2);
        if (predecessor.ProgramRevision != amendment.ProgramRevision ||
            !string.Equals(predecessor.ProgramContentSha256, amendment.ProgramContentSha256,
                StringComparison.Ordinal))
            changed.Add("program_revision");
        if (predecessor.BoundaryId != amendment.BoundaryId ||
            predecessor.ApprovedBoundaryVersionId != amendment.ApprovedBoundaryVersionId ||
            !string.Equals(predecessor.BoundaryContentSha256, amendment.BoundaryContentSha256,
                StringComparison.Ordinal))
            changed.Add("approved_boundary_version");
        return Result<IReadOnlyList<string>>.Success(changed);
    }
}
