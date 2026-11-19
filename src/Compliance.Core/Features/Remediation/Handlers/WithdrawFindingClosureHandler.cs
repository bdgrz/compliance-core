using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Remediation.Services;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Records the proposer's exact withdrawal and fences any accepted review in the same owner append.</summary>
public sealed class WithdrawFindingClosureHandler(NativeOperationExecutor operations, IAggregateReader reader, FindingReader findings,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<WithdrawFindingClosure, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<WithdrawFindingClosure> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.FindingId == Uuid.Empty || request.ClosureId == Uuid.Empty || request.ExpectedRevision < 0 || request.ProposalRevision < 1)
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Withdrawal requires the exact finding and current proposal revision."));
        var now = clock.GetUtcNow();
        var acting = await FindingActors.RequireCurrentAsync(context.Actor, request.TenantId, reader, now, ct).ConfigureAwait(false);
        if (!acting.IsSuccess)
            return Result<FindingView>.Failure(acting.Error!);
        var actor = acting.Value;
        var withdrawnBy = ActorReference.ForMember(actor.MemberId, actor.Display);
        var snapshot = new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, nameof(WithdrawFindingClosure), snapshot.Stream,
            withdrawnBy, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? result = null;

        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FindingAggregate owner, CancellationToken token)
        {
            var prepared = owner.PrepareWithdrawal(provenance, request.ClosureId, request.ProposalRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            var current = await findings.ComposeAsync(owner, now, token).ConfigureAwait(false);
            var frozen = await FindingContent.FreezeAsync(content, request.TenantId, FindingContent.ViewSchema,
                FindingViews.WithWithdrawnProposal(current!, owner.Revision + 1, withdrawnBy, now), token).ConfigureAwait(false);
            result = frozen.Result;
            return Result<FirstResponseDescriptor>.Success(frozen.Response);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId),
            owner => owner.Acceptance, PrepareAsync,
            (FindingAggregate owner, ImmutableContentRef first) => owner.WithdrawClosure(provenance, request.ClosureId,
                request.ProposalRevision, first, result!)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<FindingView>(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }
}
