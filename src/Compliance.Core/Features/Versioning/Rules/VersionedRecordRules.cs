using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
namespace Bdgrz.Compliance.Features.Versioning.Rules;

/// <summary>Shared optimistic-version and never-used-draft checks for owning aggregates.</summary>
public static class VersionedRecordRules
{
    public static VersionConflict StaleRevision(string record, long currentRevision) =>
        new(VersionConflictCode.StaleRevision, record, currentRevision);

    public static VersionConflict StaleDraft(string record, Guid currentVersionId,
        long currentRevision) => new(VersionConflictCode.StaleDraft, record,
        currentRevision, currentVersionId);

    public static VersionConflict StaleApprovedVersion(string record, Guid currentVersionId) =>
        new(VersionConflictCode.StaleApprovedVersion, record,
            CurrentVersionId: currentVersionId);

    public static VersionConflict? DraftDiscardConflict(bool everReferenced) =>
        everReferenced
            ? new VersionConflict(VersionConflictCode.ReferencedDraft, "draft")
            : null;
}
