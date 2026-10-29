using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Contracts;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
namespace Bdgrz.Compliance.Features.Work.Processes;

/// <summary>Existing named receivers with selected native identities and exact immutable causal sources.</summary>
public sealed class WeeklyDigestOperations(NativeOperationExecutor operations, IDomainEventReader events,
    ISourceFactResolver sources, IImmutableContentStore content, WeeklyWorkDigestRecipientResolver recipients, TimeProvider clock)
{
    public ValueTask<Result> DeliverAsync(IRequestContext<DeliverWeeklyWorkDigest> context, CancellationToken ct)
    {
        if (!WeeklyWorkDigestWorkerIdentity.IsNamed(context, WeeklyWorkDigest.DeliveryProcessId))
            return ValueTask.FromResult(Forbidden());
        var request = context.Request;
        if (context.CausationId != request.ScheduleEventId)
            return ValueTask.FromResult(Failure(RequestErrorKind.Conflict, "A weekly delivery requires its exact schedule cause."));
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.DigestId == Uuid.Empty ||
            request.MemberId == Uuid.Empty || request.ScheduleEventId == Uuid.Empty || request.ExpectedRevision <= 0 || request.AttemptNumber <= 0)
            return ValueTask.FromResult(Failure(RequestErrorKind.Validation, "A weekly delivery requires its selected command, owner, source and attempt."));
        return ExecuteAsync(context, request, null, ct);
    }
    public ValueTask<Result> RecordAsync(IRequestContext<RecordWorkDigestDelivery> context, CancellationToken ct)
    {
        if (!WeeklyWorkDigestWorkerIdentity.IsNamed(context, WeeklyWorkDigest.DeliveryProcessId))
            return ValueTask.FromResult(Forbidden());
        var request = context.Request;
        if (context.CausationId != request.DeliveryRequestEventId)
            return ValueTask.FromResult(Failure(RequestErrorKind.Conflict, "A weekly result requires its exact admitted request cause."));
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.DigestId == Uuid.Empty ||
            request.DeliveryRequestEventId == Uuid.Empty || request.ExpectedRevision <= 1 || request.AttemptNumber <= 0)
            return ValueTask.FromResult(Failure(RequestErrorKind.Validation, "A weekly result requires its selected command, owner, source and attempt."));
        return ExecuteAsync(context, null, request, ct);
    }
    async ValueTask<Result> ExecuteAsync(IExecutionContext context, DeliverWeeklyWorkDigest? delivery,
        RecordWorkDigestDelivery? recording, CancellationToken ct)
    {
        var tenant = delivery?.TenantId ?? recording!.TenantId;
        var digest = delivery?.DigestId ?? recording!.DigestId;
        var command = delivery?.CommandId ?? recording!.CommandId;
        var expected = delivery?.ExpectedRevision ?? recording!.ExpectedRevision;
        var attempt = delivery?.AttemptNumber ?? recording!.AttemptNumber;
        var source = delivery?.ScheduleEventId ?? recording!.DeliveryRequestEventId;
        var kind = delivery is null ? nameof(RecordWorkDigestDelivery) : nameof(DeliverWeeklyWorkDigest);
        var candidate = new WeeklyWorkDigest(tenant, digest, attempt, command, kind);
        var actor = ActorReference.ForSystemProcess(WeeklyWorkDigest.DeliveryProcessId, "Weekly work digest delivery worker");
        var attribution = RbacOperationActor.From(context.Actor);
        var intent = delivery is not null ? AuthoredIntentDigest.Compute(delivery) : AuthoredIntentDigest.Compute(recording!);
        var operation = new OperationProvenance(command, command, source, kind, actor, candidate.Stream,
            RootOperation.InitialEpisode, (ulong)expected, intent, clock.GetUtcNow(), candidate.Stream, candidate.Stream, candidate.Stream);
        WeeklyDigestPreparation? prepared = null;
        ImmutableContentRef? change = null;
        ImmutableContentRef? result = null;
        ImmutableContentRef? body = null;
        IWeeklyWorkDigestOperationFact? selected = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(WeeklyWorkDigest owner, CancellationToken token)
        {
            if (owner.Header is not { } header || delivery is not null && owner.MemberId != delivery.MemberId)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The scheduled weekly digest was not found for its exact tenant/member."));
            var key = delivery?.DeliveryKey ?? recording!.DeliveryKey;
            if (key != WeeklyWorkDigest.DeliveryKeyFor(digest, attempt) ||
                recording is not null && (recording.AttemptedAt == default || recording.AttemptedAt > operation.AcceptedAt))
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The receiver requires its exact deterministic attempt and valid completion time."));
            SourceFactProof<WeeklyWorkDigestScheduled>? scheduleProof = null;
            SourceFactProof<WeeklyWorkDigestDeliveryRequested>? requestProof = null;
            try
            {
                if (delivery is not null)
                    scheduleProof = await WeeklyDigestSources.CaptureAsync<WeeklyWorkDigestScheduled>(events, sources, content,
                        owner, source, token).ConfigureAwait(false);
                else
                    requestProof = await WeeklyDigestSources.CaptureAsync<WeeklyWorkDigestDeliveryRequested>(events, sources, content,
                        owner, source, token).ConfigureAwait(false);
            }
            catch (SourceFactVerificationException exception)
            {
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The exact weekly source proof cannot be verified: " + exception.Message, exception is SourceFactUnavailableException));
            }
            var existing = owner.SelectedAttempt is { } prior && prior.Number == attempt && prior.DeliveryKey == key;
            var suppression = delivery is null || existing ? null :
                await recipients.SuppressionReasonAsync(tenant, delivery.MemberId, token).ConfigureAwait(false);
            var mutation = new WeeklyDigestMutationContent(kind, delivery, recording, actor, attribution, operation.AcceptedAt,
                header, owner.SelectedAttempt, owner.LatestAttempt, suppression, scheduleProof, requestProof, false);
            var preparation = owner.Prepare(mutation);
            if (!preparation.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(preparation.Error!);
            prepared = preparation.Value!;
            change = await NativeContent.WriteAsync(content, tenant, WeeklyDigestNative.ChangeSchema, prepared.Change, token).ConfigureAwait(false);
            var frozen = await NativeContent.FreezeUnitAsync(content, tenant, token).ConfigureAwait(false);
            result = frozen.Result;
            body = ((StandardResponseBody)frozen.Response.Body).Content;
            return Result<FirstResponseDescriptor>.Success(frozen.Response);
        }
        var accepted = await operations.ExecuteAsync(new NativeOperation<WeeklyWorkDigest>(AcceptedOperationBinding.Of(operation),
            () => new WeeklyWorkDigest(tenant, digest, attempt, command, kind), owner =>
            {
                selected = owner.Acceptance.SelectedOperation as IWeeklyWorkDigestOperationFact;
                return owner.Acceptance;
            }, PrepareAsync, (owner, first) =>
            {
                var applied = owner.Append(prepared!, operation, change!, body!, first, result!);
                if (applied.IsSuccess)
                    selected = (IWeeklyWorkDigestOperationFact)owner.Acceptance.SelectedFact!;
                return applied;
            }), context, ct).ConfigureAwait(false);
        if (!accepted.IsSuccess)
            return Result.Failure(accepted.Error!);
        var descriptor = accepted.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null
        };
        if (descriptor is null)
            return Result.Failure(AcceptedAppendResult.ConflictError);
        if (selected is null || descriptor.HttpStatus != 200 || descriptor.ContentType != ImmutableContentStoreExtensions.JsonMediaType ||
            descriptor.ResultSchema != UnitResult.Schema || descriptor.Body is not StandardResponseBody standard ||
            standard.Content != selected.ResponseContent || !WorkDigestRetryContent.Matches(standard.Content, new UnitResult(), UnitResult.Schema))
            throw WeeklyDigestNative.Invalid("The weekly receiver has another original Unit response.");
        _ = await NativeContent.ReadAsync<UnitResult>(content, tenant, standard.Content, ct).ConfigureAwait(false);
        return Result.Success;
    }
    static Result Forbidden() => Failure(RequestErrorKind.Forbidden, "Only the existing named weekly delivery worker may invoke this receiver.");
    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
