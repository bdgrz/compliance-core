using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
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

/// <summary>Changes open corrective work for a lead or the finding's owner.</summary>
public sealed class ChangeCorrectiveActionHandler(NativeOperationExecutor operations, IAggregateReader reader,
    FindingReader findings, IPermissionAuthorizer permissions, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ChangeCorrectiveAction, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<ChangeCorrectiveAction> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.FindingId == Uuid.Empty || request.ActionId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<FindingView>.Failure(new RequestError(RequestErrorKind.Validation,
                "Changing corrective work requires its exact identities and a non-negative action revision."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var changedBy = ActorReference.ForMember(actor.MemberId, actor.Display);
        var now = clock.GetUtcNow();
        var snapshot = new CorrectiveAction(request.TenantId, request.ProgramId, request.FindingId,
            request.ActionId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, nameof(ChangeCorrectiveAction), snapshot.Stream,
            changedBy, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with
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
            if (view.OwnerMemberId != actor.MemberId && !await permissions.IsAllowedAsync(request.TenantId, actor.UserId,
                    actor.MemberId, CorrectiveActionPermissions.Change, token).ConfigureAwait(false))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Forbidden,
                    "Only the finding owner or a member with corrective-action change permission may change this action."));
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
            var invalid = FindingRules.ValidateAction(request.ActionId, request.Description, request.OwnerMemberId, request.DueOn);
            if (invalid is not null)
                return Result<FirstResponseDescriptor>.Failure(invalid.ToRequestError());
            var member = await reader.HydrateAsync(Member.ForVerification(request.TenantId, request.OwnerMemberId), token)
                .ConfigureAwait(false);
            if (!member.IsAccessEligibleOn(DateOnly.FromDateTime(now.UtcDateTime)) || member.Affiliation == "firm_staff")
                return Result<FirstResponseDescriptor>.Failure(FindingCommands.InactiveOwner());
            var changed = action with
            {
                Description = request.Description.Trim(),
                OwnerMemberId = request.OwnerMemberId,
                DueOn = request.DueOn,
                Revision = owner.Revision + 1,
            };
            (response, result) = await FindingContent.FreezeAsync(content, request.TenantId, FindingContent.ViewSchema,
                FindingViews.WithChangedAction(view, changed, changedBy, now), token).ConfigureAwait(false);
            return Result<FirstResponseDescriptor>.Success(response!);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<CorrectiveAction>(binding,
            () => new CorrectiveAction(request.TenantId, request.ProgramId, request.FindingId, request.ActionId, request.CommandId),
            action => action.Acceptance, PrepareAsync,
            (CorrectiveAction action, ImmutableContentRef frozen) => action.Change(provenance, request.Description.Trim(),
                request.OwnerMemberId, request.DueOn, changedBy, now, frozen, result!)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<FindingView>(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }
}
