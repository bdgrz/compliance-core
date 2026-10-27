using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
namespace Bdgrz.Compliance.Features.Work.Services;

/// <summary>Selected identified operator admission; current pipeline authorization precedes original-result replay.</summary>
public sealed class WorkDigestRetryOperations(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock, WorkDigestDeliverySettings settings)
{
    public async ValueTask<Result<WorkDigestDispatchStatusView>> AuthorizeAsync(
        IRequestContext<AuthorizeWorkDigestUnknownRetry> context, CancellationToken ct)
    {
        var authored = context.Request;
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var operatorId))
            return Failure(RequestErrorKind.Unauthorized, "Retry authorization requires an authenticated platform operator.");
        if (authored.CommandId == Uuid.Empty || authored.TenantId == Uuid.Empty || authored.MemberId == Uuid.Empty ||
            authored.ExpectedRevision < 0 || authored.WeekOf == default || authored.WeekOf.DayOfWeek != DayOfWeek.Monday)
            return Failure(RequestErrorKind.Validation, "An authorization requires its command id, tenant, member, Monday and owner revision.");
        var request = authored;
        var attribution = RbacOperationActor.From(context.Actor);
        var owner = new WorkDigestDispatch(request.TenantId, request.MemberId, request.WeekOf, request.CommandId);
        var operation = RootOperation.Provenance(request.CommandId, nameof(AuthorizeWorkDigestUnknownRetry), owner.Stream,
            ActorReference.ForPlatformOperator(operatorId, UserIdentityClaims.BdgrzDisplay(context.Actor, operatorId)),
            request.ExpectedRevision, AuthoredIntentDigest.Compute(request), clock.GetUtcNow()) with
        { FirstOwner = owner.Stream };
        WorkDigestRetryPreparation? prepared = null;
        ImmutableContentRef? change = null;
        ImmutableContentRef? result = null;
        ImmutableContentRef? body = null;
        WorkDigestUnknownRetryAuthorized? selected = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(WorkDigestDispatch current, CancellationToken token)
        {
            if (!AuditValue(authored.EvidenceReference, 256) || !AuditValue(authored.Rationale, 512))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                    "An evidence reference and rationale are required for retry authorization."));
            var preparation = current.PrepareRetry(request, operation, settings.MaximumAttempts);
            if (!preparation.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(preparation.Error!);
            prepared = preparation.Value!;
            var value = new WorkDigestRetryAuthorizationContent(request, operation.Actor, attribution, prepared.Before,
                prepared.RetryDeadline, settings.MaximumAttempts, operation.AcceptedAt);
            change = await NativeContent.WriteAsync(content, request.TenantId, WorkDigestRetryContent.ChangeSchema, value, token)
                .ConfigureAwait(false);
            var frozen = await NativeContent.FreezeAsync(content, request.TenantId, WorkDigestRetryContent.ResultSchema,
                prepared.Result, token).ConfigureAwait(false);
            result = frozen.Result;
            body = ((StandardResponseBody)frozen.Response.Body).Content;
            return Result<FirstResponseDescriptor>.Success(frozen.Response);
        }
        var accepted = await operations.ExecuteAsync(new NativeOperation<WorkDigestDispatch>(AcceptedOperationBinding.Of(operation),
            () => new WorkDigestDispatch(request.TenantId, request.MemberId, request.WeekOf, request.CommandId),
            current =>
            {
                selected = current.Acceptance.SelectedFact as WorkDigestUnknownRetryAuthorized;
                return current.Acceptance;
            }, PrepareAsync, (current, first) =>
            {
                var applied = current.AppendRetry(request, operation, prepared!, settings.MaximumAttempts, attribution,
                    change!, body!, first, result!);
                if (applied.IsSuccess)
                    selected = (WorkDigestUnknownRetryAuthorized)current.Acceptance.SelectedFact!;
                return applied;
            }), context, ct).ConfigureAwait(false);
        if (!accepted.IsSuccess)
            return Result<WorkDigestDispatchStatusView>.Failure(accepted.Error!);
        var descriptor = accepted.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null
        };
        if (descriptor is null)
            return Result<WorkDigestDispatchStatusView>.Failure(AcceptedAppendResult.ConflictError);
        if (selected is null || descriptor != WorkDigestRetryContent.Descriptor(selected.ResponseContent) ||
            descriptor.Body is not StandardResponseBody standard || standard.Content != selected.ResponseContent)
            throw new ImmutableContentIntegrityException("The authorization has another original response descriptor.");
        var original = await NativeContent.ReadAsync<WorkDigestDispatchStatusView>(content, request.TenantId, standard.Content, ct)
            .ConfigureAwait(false);
        if (!WorkDigestRetryContent.Matches(standard.Content, original, WorkDigestRetryContent.ResultSchema) ||
            original.TenantId != request.TenantId || original.MemberId != request.MemberId || original.WeekOf != request.WeekOf ||
            original.Status != WorkDigestDispatch.RetryPending || original.Revision != checked((long)selected.Revision) ||
            original.Attempts != selected.PriorAttempt ||
            original.MessageId != selected.MessageId || original.RetryAuthorizedBy != selected.AuthorizedBy ||
            original.RetryAuthorizedAt != selected.AuthorizedAt || original.LastUpdatedAt != selected.AuthorizedAt ||
            original.NextAttemptAt != selected.AuthorizedAt || original.RetryEvidenceReference != selected.EvidenceReference)
            throw new ImmutableContentIntegrityException("The frozen authorization result differs from its original native admission.");
        return Result<WorkDigestDispatchStatusView>.Success(original);
    }
    static bool AuditValue(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum &&
        !value.Any(char.IsControl);
    static Result<WorkDigestDispatchStatusView> Failure(RequestErrorKind kind, string message) =>
        Result<WorkDigestDispatchStatusView>.Failure(new RequestError(kind, message));
}
