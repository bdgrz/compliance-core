using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Accepts a cancellation as the root native fact of its command on the batch. The batch fact is the local
///     fence: a later commit of the batch is refused atomically with it, and a batch that already published is
///     irreversible and conflicts. <see cref="ImportCancellationReactor" /> then fences every target and releases the
///     holds. The cancellation freezes the sealed plan's manifests (empty when the batch was never sealed) under its
///     own header and reason, so every later step reads one complete intent.
/// </summary>
public sealed class CancelApplicationImportHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<CancelApplicationImport, OperationAcceptedResponse>
{
    const string CommandKind = "CancelApplicationImport";
    const string InitialEpisode = "initial";

    public async ValueTask<Result<OperationAcceptedResponse>> HandleAsync(
        IRequestContext<CancelApplicationImport> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _) || RequestActor.IsSystem(context.Actor))
            return Failure(RequestErrorKind.Forbidden, "Cancellation requires a personal Bdgrz user over HTTP.");
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.BatchId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A command id, tenant, batch and non-negative expected revision are required.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 2000)
            return Failure(RequestErrorKind.Validation, "Cancellation requires a reason of at most 2000 characters.");
        var (memberId, display) = ApplicationActor.From(context);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new ApplicationImportBatch(request.TenantId, request.BatchId,
            request.CommandId), ct).ConfigureAwait(false);
        if (!snapshot.IsCreated)
            return Failure(RequestErrorKind.NotFound, "The import batch was not found.");
        var provenance = new OperationProvenance(request.CommandId, request.CommandId, null, CommandKind,
            ActorReference.ForMember(memberId, display), snapshot.Stream, InitialEpisode,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now, snapshot.Stream,
            snapshot.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? intent = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var prepared = snapshot.PrepareCancellation(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(prepared.Error!);
            var frozen = await FreezeAsync(request, snapshot, provenance, memberId, display, now, ct)
                .ConfigureAwait(false);
            if (!frozen.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(frozen.Error!);
            intent = frozen.Value;
            response = await ApplicationImportContent.AcceptedResponseAsync(content, request.TenantId,
                request.CommandId, now, ct).ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<ApplicationImportBatch>(binding,
            () => new ApplicationImportBatch(request.TenantId, request.BatchId, request.CommandId),
            batch => batch.Acceptance,
            batch => Prepare(batch, request.ExpectedRevision, response),
            (ApplicationImportBatch batch, ImmutableContentRef acknowledgment) => batch.AcceptCancellation(provenance,
                intent!, acknowledgment)), context, ct).ConfigureAwait(false);
        return await ApplicationImportContent.AcknowledgmentAsync(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    async ValueTask<Result<ImmutableContentRef>> FreezeAsync(CancelApplicationImport request,
        ApplicationImportBatch batch, OperationProvenance provenance, Uuid memberId, string display,
        DateTimeOffset now, CancellationToken ct)
    {
        var header = new DurableIntentHeader(request.CommandId, batch.Stream, batch.Stream, batch.Stream,
            provenance.Actor, InitialEpisode, provenance.OriginalExpectedRevision, provenance.AuthoredIntentSha256, now);
        try
        {
            ImportFrozenPlanIntent intent;
            if (batch.Plan is { } sealedPlan)
            {
                var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(content, request.TenantId,
                    sealedPlan.FrozenPlan, ct).ConfigureAwait(false);
                intent = plan with { Header = header, CancellationReason = request.Reason.Trim() };
            }
            else
            {
                var empty = new ImmutableSelectionManifest(
                    await ApplicationImportContent.WriteAsync(content, request.TenantId, "import-cancellation-empty.v1",
                        Array.Empty<ApplicationImportPlannedRow>() as IReadOnlyList<ApplicationImportPlannedRow>, ct)
                        .ConfigureAwait(false), "import-planned-row.v1", 0, SelectionOrder.Authored, string.Empty);
                empty = empty with { ItemsSha256 = empty.Content.Sha256 };
                var groups = empty with { ItemSchema = ImportTargetGroupIntent.Schema };
                var claims = empty with { ItemSchema = ApplicationImportSourceClaimRecord.ItemSchema };
                intent = new ImportFrozenPlanIntent(header, batch.Id, batch.SourceKey!, batch.SourceNamespace!,
                    batch.Coverage!, batch.ContentDigest!, batch.SubmitterMemberId, memberId, empty, groups, claims,
                    batch.Proposal?.Proposal, new AuthoritativeReadCut($"stream://{batch.Stream.Realm}/{batch.Stream.Area}/{batch.Stream.Resource}",
                        batch.CommittedStreamPosition, now), batch.SubmitterDisplay!, display, 0, request.Reason.Trim());
            }

            return Result<ImmutableContentRef>.Success(await ApplicationImportContent.WriteAsync(content,
                request.TenantId, ImportFrozenPlanIntent.Schema, intent, ct).ConfigureAwait(false));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ImmutableContentRef>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The sealed plan cannot be verified yet: {exception.Message}", true));
        }
    }

    static Result<FirstResponseDescriptor> Prepare(ApplicationImportBatch batch, long expectedRevision,
        FirstResponseDescriptor? response)
    {
        var prepared = batch.PrepareCancellation(expectedRevision);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The batch changed while the cancellation was prepared; reload and retry.", true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
