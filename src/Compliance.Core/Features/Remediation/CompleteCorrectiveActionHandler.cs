using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     Records completed corrective work by the action owner or a program manager, on the action's own owner. The
///     expected revision is the revision of the action. The complete evidence list is verified content, an action held
///     by a finding closure refuses the completion until the use settles, and the frozen first response is the finding
///     view after the completion.
/// </summary>
public sealed class CompleteCorrectiveActionHandler(NativeOperationExecutor operations, IAggregateReader reader,
    FindingReader findings, OperatingAuthority authority, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CompleteCorrectiveAction, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<CompleteCorrectiveAction> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ActionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A completion requires a command id, an action and a non-negative expected revision."));
        var now = clock.GetUtcNow();
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        var snapshot = await reader.HydrateAsync(new CorrectiveAction(request.TenantId, request.ProgramId,
            request.FindingId, request.ActionId, request.CommandId), ct).ConfigureAwait(false);
        var provenance = FindingContent.Provenance(request.CommandId, "CompleteCorrectiveAction", snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? evidence = null;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        IReadOnlyList<EvidenceReference> normalized = [];
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var current = await findings.ReadOneAsync(request.TenantId, request.ProgramId, request.FindingId, now, ct)
                .ConfigureAwait(false);
            if (!current.IsSuccess)
                return current;
            var view = current.Value!;
            var action = view.CorrectiveActions.FirstOrDefault(item => item.ActionId == request.ActionId);
            if (action?.OwnerMemberId != actor.MemberId && !await authority.ManagesProgramAsync(request.TenantId, actor,
                    request.ProgramId, ct).ConfigureAwait(false))
                return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Only the corrective action owner or a program manager may complete it."));
            if (action is null)
                return Result<FindingView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The corrective action was not found."));
            if (!snapshot.IsMaterialized)
                return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The corrective action is not materialized yet; retry shortly.", true));
            var failure = FindingRules.ValidateCompletion(request.ResolutionNotes, request.Evidence);
            if (failure is not null)
                return Result<FindingView>.Failure(failure.ToRequestError());
            var prepared = snapshot.PrepareComplete(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<FindingView>.Failure(prepared.Error!);
            normalized = FindingRules.AsUnresolved(request.Evidence);
            evidence = await FindingContent.WriteAsync(content, request.TenantId, FindingContent.EvidenceSchema,
                normalized, ct).ConfigureAwait(false);
            var completed = action with
            {
                Status = FindingStates.Completed,
                ResolutionNotes = request.ResolutionNotes.Trim(),
                Evidence = normalized,
                CompletedByMemberId = actor.MemberId,
                CompletedBy = actorReference,
                CompletedAt = now,
                Revision = snapshot.Revision + 1,
            };
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId, FindingContent.ViewSchema,
                FindingViews.WithCompletedAction(view, completed, now), ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<CorrectiveAction>(binding,
            () => new CorrectiveAction(request.TenantId, request.ProgramId, request.FindingId, request.ActionId,
                request.CommandId),
            action => action.Acceptance,
            action => FindingCommands.Prepared(action.PrepareComplete(request.ExpectedRevision), response),
            (CorrectiveAction action, ImmutableContentRef frozen) => action.Complete(provenance,
                request.ResolutionNotes.Trim(), evidence!, actor.MemberId, actorReference, now, frozen, result!)),
            context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<FindingView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
