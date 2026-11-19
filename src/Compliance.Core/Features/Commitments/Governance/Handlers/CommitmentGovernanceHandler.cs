using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Governance.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Governance.Commands;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
using Bdgrz.Compliance.Features.Commitments.Governance.Services;
using Bdgrz.Compliance.Features.Independence.Contracts;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Handlers;

public sealed class CommitmentGovernanceHandler(IAggregateReader reader, IAggregateExecutor executor,
    CommitmentGovernanceAccess access, CommitmentGovernanceSources sources, ISeparationOfDutiesWaiverProofResolver waivers,
    TimeProvider clock) : IRequestHandler<DraftCommitment>, IRequestHandler<ProposeCommitmentChange>,
    IRequestHandler<ReviseCommitmentDraft>, IRequestHandler<DiscardCommitmentDraft>,
    IRequestHandler<ReviewCommitmentDraft>, IRequestHandler<RetireCommitment>
{
    public ValueTask<Result> HandleAsync(IRequestContext<DraftCommitment> context, CancellationToken ct) =>
        ExecuteAsync(context, context.Request.TenantId, context.Request.ProgramId, context.Request.CommitmentId,
            context.Request.CommitmentId, "commitments.write", context.Request.Kind, context.Request.Terms,
            (owner, actor, _, _, at) => owner.Draft(context.Request, actor, at), ct);
    public ValueTask<Result> HandleAsync(IRequestContext<ProposeCommitmentChange> context, CancellationToken ct) =>
        ExecuteAsync(context, context.Request.TenantId, context.Request.ProgramId, context.Request.CommitmentId,
            context.Request.CommandId, "commitments.write", null, context.Request.Terms,
            (owner, actor, _, _, at) => owner.Propose(context.Request, actor, at), ct);
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseCommitmentDraft> context, CancellationToken ct) =>
        ExecuteAsync(context, context.Request.TenantId, context.Request.ProgramId, context.Request.CommitmentId,
            context.Request.CommandId, "commitments.write", null, context.Request.Terms,
            (owner, actor, _, _, at) => owner.Revise(context.Request, actor, at), ct);
    public async ValueTask<Result> HandleAsync(IRequestContext<DiscardCommitmentDraft> context, CancellationToken ct)
    {
        var request = context.Request;
        var actor = await access.RequireAsync(context, request.TenantId, "commitments.write", ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error);
        var owner = await reader.HydrateAsync(new Commitment(request.TenantId, request.CommitmentId), ct).ConfigureAwait(false);
        if (!owner.IsAuthor(Uuid.Parse(actor.Value.Id, System.Globalization.CultureInfo.InvariantCulture)) && !owner.HasAccepted(request.CommandId))
        {
            var lead = await access.RequireAsync(context, request.TenantId, "commitments.manage", ct).ConfigureAwait(false);
            if (!lead.IsSuccess)
                return Result.Failure(lead.Error);
        }
        return await executor.ExecuteAsync(new Commitment(request.TenantId, request.CommitmentId),
            value => CommandFailureRequestAdapter.ToOutcome(value.Discard(request, actor.Value, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
    public ValueTask<Result> HandleAsync(IRequestContext<ReviewCommitmentDraft> context, CancellationToken ct) =>
        ExecuteAsync(context, context.Request.TenantId, context.Request.ProgramId, context.Request.CommitmentId,
            context.Request.CommandId, "commitments.approve", null, null,
            (owner, actor, boundary, waiver, at) => owner.Review(context.Request, actor, boundary, waiver, at), ct,
            context.Request.SeparationOfDutiesWaiverId);
    public ValueTask<Result> HandleAsync(IRequestContext<RetireCommitment> context, CancellationToken ct) =>
        ExecuteAsync(context, context.Request.TenantId, context.Request.ProgramId, context.Request.CommitmentId,
            context.Request.CommandId, "commitments.approve", null, null,
            (owner, actor, _, _, at) => owner.Retire(context.Request, actor, at), ct);
    async ValueTask<Result> ExecuteAsync(IRequestContext context, Uuid tenant, Uuid program, Uuid id, Uuid command,
        string permission, string? kind, CommitmentTerms? proposed,
        Func<Commitment, ActorReference, Uuid, SeparationOfDutiesWaiverProof?, DateTimeOffset, CommandFailure?> apply,
        CancellationToken ct, Uuid? waiverId = null)
    {
        var actor = await access.RequireAsync(context, tenant, permission, ct).ConfigureAwait(false);
        if (!actor.IsSuccess)
            return Result.Failure(actor.Error);
        var owner = await reader.HydrateAsync(new Commitment(tenant, id), ct).ConfigureAwait(false);
        if (owner.Revision > 0 && owner.ProgramId != program)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The commitment was not found in this program."));
        var now = clock.GetUtcNow();
        var boundary = Uuid.Empty;
        SeparationOfDutiesWaiverProof? waiver = null;
        if (!owner.HasAccepted(command))
        {
            var terms = proposed ?? (permission == "commitments.approve" && context.Request is ReviewCommitmentDraft ? owner.OpenDraft : null);
            if (terms is not null)
            {
                var source = await sources.RequireAsync(tenant, program, kind ?? owner.Kind, terms, ct).ConfigureAwait(false);
                if (!source.IsSuccess)
                    return Result.Failure(source.Error);
                boundary = source.Value;
            }
            if (waiverId is { } selected)
            {
                var member = Uuid.Parse(actor.Value.Id, System.Globalization.CultureInfo.InvariantCulture);
                var proof = await waivers.ResolveAsync(new(tenant, selected, member, member, SeparationOfDutiesActions.Approve,
                    SeparationOfDutiesRecordTypes.Commitment, id, id, owner.Revision), now, ct).ConfigureAwait(false);
                if (!proof.IsSuccess)
                    return Result.Failure(proof.Error);
                waiver = proof.Value;
            }
        }
        return await executor.ExecuteAsync(new Commitment(tenant, id),
            value => CommandFailureRequestAdapter.ToOutcome(apply(value, actor.Value, boundary, waiver, now)), context, ct).ConfigureAwait(false);
    }
}
