using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Governance.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Governance.Commands;
using Bdgrz.Compliance.Features.Commitments.Governance.Services;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Handlers;

public sealed class CommitmentControlLinkHandler(IAggregateReader reader, IAggregateExecutor executor,
    ControlReader controls, CommitmentGovernanceAccess access, TimeProvider clock)
    : IRequestHandler<LinkCommitmentToControl>, IRequestHandler<ConfirmCommitmentControlLink>, IRequestHandler<UnlinkCommitmentFromControl>
{
    public ValueTask<Result> HandleAsync(IRequestContext<LinkCommitmentToControl> context, CancellationToken ct) =>
        ExecuteAsync(context, context.Request.CommandId, context.Request.TenantId, context.Request.ProgramId,
            context.Request.CommitmentId, context.Request.ControlId, false,
            (owner, commitment, control, actor, at) => owner.Link(context.Request, commitment, control, actor, at), ct);
    public ValueTask<Result> HandleAsync(IRequestContext<ConfirmCommitmentControlLink> context, CancellationToken ct) =>
        ExecuteAsync(context, context.Request.CommandId, context.Request.TenantId, context.Request.ProgramId,
            context.Request.CommitmentId, context.Request.ControlId, false,
            (owner, commitment, control, actor, at) => owner.Confirm(context.Request, commitment, control, actor, at), ct);
    public ValueTask<Result> HandleAsync(IRequestContext<UnlinkCommitmentFromControl> context, CancellationToken ct) =>
        ExecuteAsync(context, context.Request.CommandId, context.Request.TenantId, context.Request.ProgramId,
            context.Request.CommitmentId, context.Request.ControlId, true,
            (owner, _, _, actor, at) => owner.Unlink(context.Request, actor, at), ct);
    async ValueTask<Result> ExecuteAsync(IRequestContext context, Uuid command, Uuid tenant, Uuid program, Uuid commitment,
        Uuid control, bool unlink, Func<CommitmentControlLink, long, long, ActorReference, DateTimeOffset, CommandFailure?> apply,
        CancellationToken ct)
    {
        var actor = await access.RequireAsync(context, tenant, "commitments.link", ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error);
        var owner = await reader.HydrateAsync(new CommitmentControlLink(tenant, commitment, control), ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var selectedCommitment = 0L;
        var selectedControl = 0L;
        if (!unlink && !owner.HasAccepted(command))
        {
            var promise = await reader.HydrateAsync(new Commitment(tenant, commitment), ct).ConfigureAwait(false);
            var operation = await controls.ReadAsync(tenant, program, control, ct).ConfigureAwait(false);
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var current = promise.Versions.LastOrDefault(value => value.EffectiveOn <= today);
            var version = operation.ReadApprovedVersionHistory().LastOrDefault(value => value.EffectiveFrom <= today &&
                (value.EffectiveUntil is null || value.EffectiveUntil > today));
            if (promise.ProgramId != program || !promise.IsActiveOn(today) || current is null || operation.IsRetired || version is null)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict, "Both linked records must be active and approved in this program."));
            selectedCommitment = current.Version; selectedControl = version.Revision;
        }
        return await executor.ExecuteAsync(new CommitmentControlLink(tenant, commitment, control),
            value => CommandFailureRequestAdapter.ToOutcome(apply(value, selectedCommitment, selectedControl, actor.Value, now)), context, ct).ConfigureAwait(false);
    }
}
