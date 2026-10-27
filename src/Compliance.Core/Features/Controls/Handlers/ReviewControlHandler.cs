using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Controls.Commands;
namespace Bdgrz.Compliance.Features.Controls.Handlers;

public sealed class ReviewControlHandler(ControlOperations operations, ControlActivationReleaseGate releaseGate)
    : IRequestHandler<ReviewControl>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewControl> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Control decision requires a personal HTTP invocation.")));
        var request = context.Request;
        SeparationOfDutiesWaiverProof? waiver = null;
        return operations.ExecuteUnitAsync(context, request.TenantId, request.ProgramId, request.ControlId,
            request.CommandId, nameof(ReviewControl), request.ExpectedRevision,
            async (snapshot, actor, at, token) =>
            {
                if (!releaseGate.IsEnabled)
                    return Result.Failure(ControlActivationReleaseGate.Unavailable);
                if (snapshot.PendingTargetId is not { } target)
                    return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The control has no pending decision target."));
                var proof = await operations.ResolveWaiverAsync(request.TenantId, request.SeparationOfDutiesWaiverId,
                    actor.MemberId, request.ControlId, target, request.ExpectedRevision, SeparationOfDutiesActions.Review, at, token)
                    .ConfigureAwait(false);
                waiver = proof.IsSuccess ? proof.Value : null;
                return proof.IsSuccess ? Result.Success : Result.Failure(proof.Error!);
            },
            (rules, actor, at) => ControlOperations.Outcome(rules.Review(request.ProgramId, request.ExpectedRevision,
                request.CommandId, request.Outcome, request.Rationale, actor.MemberId, actor.Display, at, waiver)),
            null, () => waiver is null ? [] : [waiver], ct);
    }
}
