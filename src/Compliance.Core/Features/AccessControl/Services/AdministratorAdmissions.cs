using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Checks shared by the guard-first administrator routes.</summary>
static class AdministratorAdmissions
{
    /// <summary>The error a route reports while another change to the same source owner of the episode is unfinished.</summary>
    public static RequestError InProgress { get; } = new(RequestErrorKind.Conflict,
        "Another change to this member is in progress.", true);

    /// <summary>The acting member of a Bdgrz user, as the durable actor of the admitted operation.</summary>
    public static ActorReference? MemberActor(ClaimsPrincipal actor, Uuid tenantId)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(actor, out var userId))
            return null;
        return ActorReference.ForMember(RbacIds.Member(tenantId, userId), UserIdentityClaims.BdgrzDisplay(actor, userId));
    }

    /// <summary>
    ///     Fails transiently while an unfinished operation of the episode lands on the same source owner and the new
    ///     one cannot wait behind it. <paramref name="allowedBehind" /> names the pending operations it may follow.
    /// </summary>
    public static Result Unfinished(TenantManagerGuard guard, Uuid episode, bool memberOwned,
        Func<PendingTransition, bool> allowedBehind) =>
        guard.PendingFor(episode).Any(pending =>
            (memberOwned ? pending.OwnedByMember : pending.OwnedByAssignment) && !allowedBehind(pending))
            ? Result.Failure(InProgress)
            : Result.Success;
}
