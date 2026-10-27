using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

/// <summary>Applies the administrators-team removal the guard admitted as neutral, at the revision it pinned.</summary>
public sealed class ApplyNeutralAdministratorAssignmentMutationHandler(AdministratorReceiverSupport support)
    : IRequestHandler<ApplyNeutralAdministratorAssignmentMutation, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<ApplyNeutralAdministratorAssignmentMutation> context, CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyAdmissionAsync(request.Source, request, request.MemberEpisode,
            static kind => kind == AdministratorTransitionKind.NeutralAssignmentMutation, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = verified.Value!.Intent;
        var tenantId = AdministratorReceiverSupport.TenantOf(request.Target);
        var teamId = BuiltInRbac.AdministratorsTeamId(tenantId);
        var stream = new TeamMember(tenantId, teamId, request.MemberId, request.CommandId).Stream;
        if (request.Target != stream || intent.AssignmentId != request.AssignmentId || intent.AssignmentGeneration is null)
            return AdministratorReceiverSupport.Invalid<ReceiverOutcome>("The command does not match the admitted removal.");
        var provenance = AdministratorReceiverSupport.Provenance(request,
            nameof(ApplyNeutralAdministratorAssignmentMutation), stream, intent, verified.Value.FactEventId);
        var generation = (long)intent.AssignmentGeneration.Value;
        return await support.ApplyAsync(() => new TeamMember(tenantId, teamId, request.MemberId, request.CommandId),
            assignment => assignment.Acceptance, AcceptedOperationBinding.Of(provenance),
            assignment => AdministratorSupersession.Of(assignment, request.MemberEpisode),
            (TeamMember assignment) => assignment.ApplyRemoval(provenance, request.MemberEpisode, request.ExpectedRevision ?? -1,
                generation), context, ct).ConfigureAwait(false);
    }
}
