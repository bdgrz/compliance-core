using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Versioning.Services;

/// <summary>Maps Compliance conflict facts at the Portia result boundary.</summary>
public static class VersionConflictRequestErrors
{
    public static RequestError ToRequestError(this VersionConflict conflict) =>
        new(RequestErrorKind.Conflict, conflict.Code switch
        {
            VersionConflictCode.StaleRevision =>
                $"The {conflict.Record} changed. Current revision: {conflict.CurrentRevision}. Reload it and retry.",
            VersionConflictCode.StaleDraft =>
                $"The {conflict.Record} draft changed. Current draft version: {conflict.CurrentVersionId}; revision: {conflict.CurrentRevision}.",
            VersionConflictCode.StaleApprovedVersion =>
                $"The {conflict.Record} has a different approved version. Current approved version: {conflict.CurrentVersionId}.",
            VersionConflictCode.ReferencedDraft =>
                "A reviewed draft is referenced by an immutable decision and cannot be discarded.",
            _ => throw new ArgumentOutOfRangeException(nameof(conflict)),
        });
}
