using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Contracts;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Records a lead's reason for cancelling an open corrective action.</summary>
public sealed class CancelCorrectiveActionHandler(NativeOperationExecutor operations, FindingReader findings,
    IPermissionAuthorizer permissions, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CancelCorrectiveAction, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<CancelCorrectiveAction> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ActionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A cancellation requires a command id, an action and a non-negative expected revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var actorReference = ActorReference.ForMember(actor.MemberId, actor.Display);
        if (!await permissions.IsAllowedAsync(request.TenantId, actor.UserId, actor.MemberId,
                CorrectiveActionPermissions.Cancel, ct).ConfigureAwait(false))
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only a member with corrective-action cancellation permission may cancel it."));
        var now = clock.GetUtcNow();
        var snapshot = new CorrectiveAction(request.TenantId, request.ProgramId, request.FindingId,
            request.ActionId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, nameof(CancelCorrectiveAction), snapshot.Stream,
            actorReference, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with
        { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(CorrectiveAction owner, CancellationToken token)
        {
            var current = await findings.ReadOneAsync(request.TenantId, request.ProgramId, request.FindingId, now, token)
                .ConfigureAwait(false);
            if (!current.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(current.Error!);
            var view = current.Value!;
            var action = view.CorrectiveActions.FirstOrDefault(item => item.ActionId == request.ActionId);
            if (action is null)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The corrective action was not found."));
            if (!owner.IsMaterialized)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The corrective action is not materialized yet; retry shortly.", true));
            var prepared = owner.PrepareComplete(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            if (!FindingRules.IsBoundedText(request.Reason, FindingRules.MaximumTextLength))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                    "Cancelling a corrective action requires a reason of at most 4000 characters."));
            var cancelled = action with
            {
                Status = FindingStates.Cancelled,
                CancellationReason = request.Reason.Trim(),
                CancelledByMemberId = actor.MemberId,
                CancelledBy = actorReference,
                CancelledAt = now,
                Revision = owner.Revision + 1,
            };
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId, FindingContent.ViewSchema,
                FindingViews.WithCancelledAction(view, cancelled, now), token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<CorrectiveAction>(binding,
            () => new CorrectiveAction(request.TenantId, request.ProgramId, request.FindingId,
                request.ActionId, request.CommandId), action => action.Acceptance, PrepareAsync,
            (CorrectiveAction action, ImmutableContentRef frozen) => action.Cancel(provenance, request.Reason.Trim(),
                actor.MemberId, actorReference, now, frozen, result!)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<FindingView>(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
