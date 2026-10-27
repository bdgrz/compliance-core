using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Records an attributable severity, ownership, due-date, scope, or root-cause change. The frozen first response is
///     the complete finding view after the fact, so an exact retry returns the original view even after later changes.
/// </summary>
public sealed class ReviseFindingHandler(NativeOperationExecutor operations, IAggregateReader reader, FindingReader findings, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviseFinding, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<ReviseFinding> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A revision requires a command id and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = new FindingAggregate(request.TenantId, request.ProgramId,
            request.FindingId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, "ReviseFinding", snapshot.Stream, actorReference,
            (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);

        FindingRevision? revision = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FindingAggregate owner, CancellationToken token)
        {
            var prepared = owner.PrepareWrite(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            var failure = FindingRules.ValidateRevision(request.Severity, request.OwnerMemberId, request.DueOn,
                request.AffectedScope, request.RootCause, request.Reason);
            if (failure is not null)
                return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
            var cause = FindingRules.Clean(request.RootCause);
            if (owner.Severity == request.Severity && owner.OwnerMemberId == request.OwnerMemberId &&
                owner.DueOn == request.DueOn && owner.AffectedScope == request.AffectedScope.Trim() &&
                owner.RootCause == cause)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The finding revision changes nothing."));
            if (!await FindingCommands.IsActiveMemberAsync(reader, request.TenantId, request.OwnerMemberId, token)
                    .ConfigureAwait(false))
                return Result<FirstResponseDescriptor>.Failure(FindingCommands.InactiveOwner());
            revision = new FindingRevision(request.Severity, request.OwnerMemberId, request.DueOn,
                request.AffectedScope.Trim(), cause, request.Reason.Trim(), actorReference, now);
            var current = await findings.ComposeAsync(owner, now, token).ConfigureAwait(false);
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId, FindingContent.ViewSchema,
                FindingViews.WithRevision(current!, revision, owner.Revision + 1, now), token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId),
            finding => finding.Acceptance,
            PrepareAsync,
            (FindingAggregate finding, ImmutableContentRef frozen) =>
                finding.Revise(provenance, revision!, frozen, result!)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<FindingView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
