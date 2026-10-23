using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Removes the administrators-team assignment a withdrawal admitted, at the revision and generation it pinned.</summary>
public sealed class ApplyAdministratorAssignmentWithdrawalHandler(AdministratorReceiverSupport support)
    : IRequestHandler<ApplyAdministratorAssignmentWithdrawal, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<ApplyAdministratorAssignmentWithdrawal> context, CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyAdmissionAsync(request.Source, request, request.MemberEpisode,
            static kind => kind == AdministratorTransitionKind.RemoveAdministratorsAssignment, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = verified.Value!.Intent;
        var tenantId = AdministratorReceiverSupport.TenantOf(request.Target);
        var teamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        var stream = new TeamMember(tenantId, teamId, request.MemberId, request.CommandId).Stream;
        if (request.Target != stream || intent.AssignmentId != request.AssignmentId || intent.AssignmentGeneration is null)
            return AdministratorReceiverSupport.Invalid<ReceiverOutcome>("The command does not match the admitted removal.");
        var provenance = AdministratorReceiverSupport.Provenance(request, nameof(ApplyAdministratorAssignmentWithdrawal),
            stream, intent, verified.Value.FactEventId);
        var generation = (long)intent.AssignmentGeneration.Value;
        return await support.ApplyAsync(() => new TeamMember(tenantId, teamId, request.MemberId, request.CommandId),
            assignment => assignment.Acceptance, AcceptedOperationBinding.Of(provenance),
            assignment => AdministratorSupersession.Of(assignment, request.MemberEpisode),
            (TeamMember assignment) => assignment.ApplyRemoval(provenance, request.MemberEpisode, request.ExpectedRevision ?? -1,
                generation), context, ct).ConfigureAwait(false);
    }
}
