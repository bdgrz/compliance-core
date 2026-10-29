using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

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
