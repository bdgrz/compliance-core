using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>Source-record checks shared by plan proposal and preview.</summary>
static class OperatingPlanChecks
{
    public static async ValueTask<Result<(ControlVersionView Version, bool ReviewerHoldsWork)>>
        CheckAsync(ControlReader controlSources, OperatingAuthority authority, Uuid tenantId,
            Uuid programId, Uuid controlId, Uuid controlVersionId, OperatingHolder owner,
            OperatingHolder? backupOwner, Uuid reviewerMemberId, CancellationToken ct)
    {
        var control = await ControlOperationsSource.LoadControlAsync(controlSources, tenantId, programId,
            controlId, ct).ConfigureAwait(false);
        if (control is null)
            return Failure(ControlOperationsSource.ControlNotFound());
        if (control.IsRetired || control.ApprovedVersion is not { Status: "approved" } version)
            return Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an active control with an approved version can receive an operating plan."));
        if (version.VersionId != controlVersionId)
            return Failure(new RequestError(RequestErrorKind.Conflict,
                "An operating plan must target the control's exact current approved version."));
        foreach (var holder in new[] { owner, backupOwner,
                     new OperatingHolder(OperatingAuthority.MemberHolder, reviewerMemberId) })
            if (holder is not null && holder.Id != Uuid.Empty &&
                !await authority.IsActiveAsync(tenantId, holder, ct).ConfigureAwait(false))
                return Failure(new RequestError(RequestErrorKind.Validation,
                    "Every operating holder must be an active member, an existing workforce person, or an active team."));
        var reviewerHoldsWork =
            await authority.HoldsAsync(tenantId, owner, reviewerMemberId, ct).ConfigureAwait(false) ||
            await authority.HoldsAsync(tenantId, backupOwner, reviewerMemberId, ct)
                .ConfigureAwait(false);
        return Result<(ControlVersionView, bool)>.Success((version, reviewerHoldsWork));
    }

    static Result<(ControlVersionView, bool)> Failure(RequestError error) =>
        Result<(ControlVersionView, bool)>.Failure(error);
}
