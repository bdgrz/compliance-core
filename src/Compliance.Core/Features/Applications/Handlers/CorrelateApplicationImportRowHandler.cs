using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Records one personal correlation decision as the root native acceptance of its command on the batch. The
///     command identity is the logical operation, so an exact retry returns the original result even after later
///     changes. The guards are the staged row, the committed source claims, the linked application and the batch's
///     own revision.
/// </summary>
public sealed class CorrelateApplicationImportRowHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock, IDomainEventReader events) : IRequestHandler<CorrelateApplicationImportRow>
{
    const string CommandKind = "CorrelateApplicationImportRow";
    const string InitialEpisode = "initial";

    public async ValueTask<Result> HandleAsync(IRequestContext<CorrelateApplicationImportRow> context,
        CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact import source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result> HandleCoreAsync(IRequestContext<CorrelateApplicationImportRow> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _) || RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Correlation requires a personal Bdgrz user over HTTP."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.BatchId == Uuid.Empty ||
            request.RowId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, tenant, batch, row and non-negative expected revision are required."));
        var (memberId, display) = ApplicationActor.From(context);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new ApplicationImportBatch(request.TenantId, request.BatchId,
            request.CommandId), ct).ConfigureAwait(false);
        var provenance = new OperationProvenance(request.CommandId, request.CommandId, null, CommandKind,
            ActorReference.ForMember(memberId, display), snapshot.Stream, InitialEpisode, (ulong)request.ExpectedRevision,
            AuthoredIntentDigest.Compute(request), now, snapshot.Stream, snapshot.Stream, snapshot.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        string? sourceRecordId = null;
        Uuid targetId = default;
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        ApplicationImportBatchPreparedCut? preparedCut = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<ApplicationImportBatch>(binding,
            () => new ApplicationImportBatch(request.TenantId, request.BatchId, request.CommandId),
            batch => batch.Acceptance,
            async (batch, prepareCt) =>
            {
                var prepared = batch.PrepareCorrelation(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var checkedRow = await CheckAsync(request, batch, prepareCt).ConfigureAwait(false);
                if (!checkedRow.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(checkedRow.Error!);
                (sourceRecordId, targetId) = checkedRow.Value;
                var unit = await ApplicationImportContent.UnitAsync(content, request.TenantId, prepareCt).ConfigureAwait(false);
                (response, result) = (unit.Response, unit.Result);

                preparedCut = ApplicationImportBatchPreparedCut.Capture(batch);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (ApplicationImportBatch batch, ImmutableContentRef frozen) =>
            {
                if (preparedCut?.Matches(batch) != true)
                    return ApplicationImportBatchPreparedCut.Changed();
                return batch.Correlate(provenance, request.RowId,
                sourceRecordId!, request.Decision, targetId, request.ExpectedApplicationRevision,
                request.Reason.Trim(), memberId, display, now, frozen, result!);
            }), context, ct).ConfigureAwait(false);
        return ApplicationImportContent.UnitOutcome(outcome);
    }

    async ValueTask<Result<(string SourceRecordId, Uuid TargetId)>> CheckAsync(CorrelateApplicationImportRow request,
        ApplicationImportBatch batch, CancellationToken ct)
    {
        if (!batch.IsCreated || batch.Staged is not { } staged)
            return Fail(RequestErrorKind.NotFound, "The import batch was not found.");
        var history = await ApplicationImportBatchHistory.ReadAsync(events, batch, ct).ConfigureAwait(false);
        IReadOnlyList<ApplicationImportStagedRow> rows;
        try
        {
            rows = await ApplicationImportBatchRows.ReadAsync(content, batch, history, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Fail(RequestErrorKind.Conflict, "The staged rows cannot be verified.", error is ImmutableContentUnavailableException);
        }

        var row = rows.FirstOrDefault(candidate => candidate.RowId == request.RowId);
        if (row is null)
            return Fail(RequestErrorKind.NotFound, "The import row was not found.");
        if (batch.IsCancelRequested || batch.IsEnded)
            return Fail(RequestErrorKind.Conflict, "The import batch is canceled or failed.");
        if (row.ValidationFindings.Count > 0 || string.IsNullOrWhiteSpace(row.SourceRecordId))
            return Fail(RequestErrorKind.Validation, "An invalid or duplicate row cannot be correlated.");
        if (request.Decision is not ("link_existing" or "create_new") ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000 ||
            (request.Decision == "create_new" && (request.ApplicationId is not null || request.ExpectedApplicationRevision is not null)) ||
            (request.Decision == "link_existing" && (request.ApplicationId is null || request.ApplicationId == Uuid.Empty || request.ExpectedApplicationRevision is null or < 1)))
            return Fail(RequestErrorKind.Validation,
                "Correlation requires an explicit bounded decision and target revision for an existing application.");
        var targetId = request.ApplicationId ?? ApplicationImportEffects.CreatedApplicationId(batch.Id, row.RowId);
        var source = await ApplicationImportSourceView.LoadAsync(reader, content, events, request.TenantId, batch.SourceKey!,
            batch.SourceNamespace!, ct: ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<(string, Uuid)>.Failure(source.Error!);
        if (source.Value!.Find(row.SourceRecordId!) is { } claim &&
            (targetId != claim.Observation.ApplicationId || request.Decision != "link_existing"))
            return Fail(RequestErrorKind.Conflict, "A committed source claim cannot be rebound to another application.");
        if (batch.Plan is not null)
            return Fail(RequestErrorKind.Conflict, "The import plan is already frozen.");
        if (request.Decision == "link_existing")
        {
            var application = await reader.HydrateApplicationAsync(request.TenantId, request.ApplicationId!.Value, ct)
                .ConfigureAwait(false);
            if (!application.IsCreated || application.Stream.Realm != request.TenantId.ToString() ||
                application.Id != request.ApplicationId)
                return Fail(RequestErrorKind.NotFound, "The application was not found.");
            if (application.IsRetired)
                return Fail(RequestErrorKind.Conflict, "The application is retired.");
            if (application.Revision != request.ExpectedApplicationRevision)
                return Result<(string, Uuid)>.Failure(
                    VersionedRecordRules.StaleRevision("application", application.Revision).ToRequestError());
        }

        return Result<(string, Uuid)>.Success((row.SourceRecordId!, targetId));
    }

    static Result<(string SourceRecordId, Uuid TargetId)> Fail(RequestErrorKind kind, string message,
        bool transient = false) => Result<(string, Uuid)>.Failure(new RequestError(kind, message, transient));
}
