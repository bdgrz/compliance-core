using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Responsibilities.Services;

public static class ResponsibilityDecisionGuard
{
    public static CommandFailure? Validate(ResponsibilitySet set, ResponsibilityScope scope,
        Uuid memberId, ResponsibilityType decisionType, DateTimeOffset at,
        bool isRecordAuthor, SeparationOfDutiesWaiver? waiver)
    {
        return ValidateCore(set, scope, memberId, decisionType, at, isRecordAuthor, waiver is not null,
            exact => waiver?.TenantId == set.TenantId && waiver.Allows(exact, memberId, at));
    }

    public static CommandFailure? ValidateAuthorized(ResponsibilitySet set, ResponsibilityScope scope,
        Uuid memberId, ResponsibilityType decisionType, DateTimeOffset at,
        bool isRecordAuthor, SeparationOfDutiesWaiverProof? waiver)
    {
        return ValidateCore(set, scope, memberId, decisionType, at, isRecordAuthor, waiver is not null,
            exact => waiver is not null && Bdgrz.Compliance.Features.Controls.Services.ControlWaiverProof.Allows(waiver,
                set.TenantId, exact, memberId, at));
    }

    static CommandFailure? ValidateCore(ResponsibilitySet set, ResponsibilityScope scope,
        Uuid memberId, ResponsibilityType decisionType, DateTimeOffset at,
        bool isRecordAuthor, bool hasWaiver, Func<SeparationOfDutiesWaiverScope, bool> authorizes)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (decisionType is not (ResponsibilityType.AssignedReviewer or
            ResponsibilityType.PolicyApprover))
            return CommandFailure.InvalidContent(
                "A responsibility decision must be a review or policy approval.");
        var proposed = new ResponsibilityAssignmentView(set.TenantId, Uuid.Empty, memberId,
            decisionType, scope, at, Uuid.Empty, at, at.AddTicks(1), null, Uuid.Empty, []);
        var conflicts = ResponsibilityConflictPolicy.FindConflicts(set.ReadAssignments(), proposed);
        var action = decisionType == ResponsibilityType.AssignedReviewer
            ? SeparationOfDutiesActions.Review
            : SeparationOfDutiesActions.Approve;
        if (conflicts.Count > 0)
        {
            var exactScope = new SeparationOfDutiesWaiverScope(scope.RecordType, scope.RecordId,
                scope.VersionId, scope.Revision, action);
            return authorizes(exactScope)
                ? null
                : CommandFailure.ActorProhibited(
                    "The member has a conflicting responsibility and requires an active exact-scope waiver.");
        }
        return isRecordAuthor || !hasWaiver
            ? null
            : CommandFailure.ActorProhibited(
                "A separation-of-duties waiver may only be used for a current conflict.");
    }
}
