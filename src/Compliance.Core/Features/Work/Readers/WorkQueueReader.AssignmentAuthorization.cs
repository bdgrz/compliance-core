using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Operations.Models;
namespace Bdgrz.Compliance.Features.Work.Readers;

public sealed partial class WorkQueueReader
{
    internal async ValueTask<Result> AuthorizeAssignmentActorAsync(Uuid tenantId, OperationsActor actor, CancellationToken ct)
    {
        var member = await reader.HydrateAsync(Member.ForVerification(tenantId, actor.MemberId), ct).ConfigureAwait(false);
        return member.IsRegistered && !member.IsSuspended && !member.IsDeprovisioned && member.Affiliation != "firm_staff" &&
            member.UserId == actor.UserId && RbacIds.Member(tenantId, actor.UserId) == actor.MemberId
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current active canonical client membership is required."));
    }

    internal async ValueTask<Result> AuthorizeAssignmentReceiptAsync(Uuid tenantId, Uuid programId, OperationsActor actor,
        WorkCandidate source, string commandKind, CancellationToken ct)
    {
        var actorAuthorization = await AuthorizeAssignmentActorAsync(tenantId, actor, ct).ConfigureAwait(false);
        if (!actorAuthorization.IsSuccess)
            return actorAuthorization;
        if (!await CanReadSourceAsync(tenantId, source, actor.MemberId, ct).ConfigureAwait(false) ||
            !await CanAssignAsync(tenantId, source, actor.MemberId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current source visibility and management independence are required for the original receipt."));
        if ((RiskAcceptanceWork.HasRestrictedAuthority(source) || source.Kind == FindingClosureWork.Kind) &&
            !await IsEligibleAsync(tenantId, source, actor.MemberId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current restricted source decision authority is required for this original receipt."));
        if (commandKind == nameof(AssignWorkItem))
            return await authority.ManagesProgramAsync(tenantId, actor, programId, ct).ConfigureAwait(false)
                ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current program management authority is required for the original assignment receipt."));
        if (commandKind == nameof(ClaimWorkItem) && (source.Responsible.Kind != OperatingAuthority.TeamHolder ||
                !await HoldsAsync(tenantId, source.Responsible, actor.MemberId, ct).ConfigureAwait(false)))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current membership of the source holding team is required for this claim receipt."));
        return await IsEligibleAsync(tenantId, source, actor.MemberId, ct).ConfigureAwait(false)
            ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Current source operating authority is required for this original receipt."));
    }
}
