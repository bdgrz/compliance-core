using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Seals the preparer's omission proposal as the root native acceptance of its command on the batch. The
///     complete proposal is immutable content bound to the exact source position and content digest; it is never
///     authority to retire. A batch needs no cap on the number of omissions.
/// </summary>
public sealed class FreezeApplicationImportOmissionProposalHandler(NativeOperationExecutor operations,
    IAggregateReader reader, IImmutableContentStore content, TimeProvider clock, IDomainEventReader events)
    : IRequestHandler<FreezeApplicationImportOmissionProposal>
{
    const string CommandKind = "FreezeApplicationImportOmissionProposal";
    const string InitialEpisode = "initial";

    public async ValueTask<Result> HandleAsync(IRequestContext<FreezeApplicationImportOmissionProposal> context,
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

    async ValueTask<Result> HandleCoreAsync(IRequestContext<FreezeApplicationImportOmissionProposal> context, CancellationToken ct)
    {
        if (context.Invocation is not HttpInvocation || RequestActor.IsSystem(context.Actor) ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out _))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Omission proposal confirmation requires a personal Bdgrz user over HTTP."));
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.BatchId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, tenant, batch and non-negative expected revision are required."));
        var (memberId, display) = ApplicationActor.From(context);
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new ApplicationImportBatch(request.TenantId, request.BatchId,
            request.CommandId), ct).ConfigureAwait(false);
        var provenance = new OperationProvenance(request.CommandId, request.CommandId, null, CommandKind,
            ActorReference.ForMember(memberId, display), snapshot.Stream, InitialEpisode,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now, snapshot.Stream,
            snapshot.Stream, snapshot.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        FirstResponseDescriptor? response = null;
        ImmutableContentRef? result = null;
        ImmutableContentRef? proposal = null;
        ApplicationImportBatchPreparedCut? preparedCut = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<ApplicationImportBatch>(binding,
            () => new ApplicationImportBatch(request.TenantId, request.BatchId, request.CommandId),
            batch => batch.Acceptance,
            async (batch, prepareCt) =>
            {
                var prepared = batch.PrepareProposal(request.ExpectedRevision);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                var frozen = await FreezeAsync(request, batch, memberId, display, now, prepareCt).ConfigureAwait(false);
                if (!frozen.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(frozen.Error!);
                proposal = frozen.Value;
                var unit = await ApplicationImportContent.UnitAsync(content, request.TenantId, prepareCt).ConfigureAwait(false);
                (response, result) = (unit.Response, unit.Result);

                preparedCut = ApplicationImportBatchPreparedCut.Capture(batch);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (ApplicationImportBatch batch, ImmutableContentRef frozen) =>
            {
                if (preparedCut?.Matches(batch) != true)
                    return ApplicationImportBatchPreparedCut.Changed();
                return batch.SealProposal(provenance,
                proposal!.Sha256, proposal, frozen, result!);
            }), context, ct).ConfigureAwait(false);
        return ApplicationImportContent.UnitOutcome(outcome);
    }

    async ValueTask<Result<ImmutableContentRef>> FreezeAsync(FreezeApplicationImportOmissionProposal request,
        ApplicationImportBatch batch, Uuid memberId, string display, DateTimeOffset now, CancellationToken ct)
    {
        if (!batch.IsCreated || batch.Staged is not { } staged)
            return Fail(RequestErrorKind.NotFound, "The import batch was not found.");
        if (string.IsNullOrWhiteSpace(display) || display.Length > 256 ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000)
            return Fail(RequestErrorKind.Validation, "A retirement proposal requires a bounded attributable decision.");
        if (request.ExpectedContentSha256 != batch.ContentDigest)
            return Fail(RequestErrorKind.Conflict, "The import content digest changed.");
        if (!batch.IsPreparing)
            return Fail(RequestErrorKind.Conflict, "The import batch is not available for a retirement proposal.");
        var prepared = batch.PrepareProposal(request.ExpectedRevision);
        if (!prepared.IsSuccess)
            return Result<ImmutableContentRef>.Failure(prepared.Error!);
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

        var source = await ApplicationImportSourceView.LoadAsync(reader, content, events, request.TenantId, batch.SourceKey!,
            batch.SourceNamespace!, ct: ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<ImmutableContentRef>.Failure(source.Error!);
        var view = source.Value!;
        if (request.ExpectedSourcePosition != view.Source.CommittedStreamPosition ||
            view.Source.Version != view.Source.CommittedStreamPosition || batch.CommittedStreamPosition == 0)
            return Fail(RequestErrorKind.Conflict,
                "The source must be durable and unchanged before freezing omissions.", true);
        if (view.Source.ActiveBatchId is not null)
            return Fail(RequestErrorKind.Conflict,
                "The source must be durable and unchanged before freezing omissions.", true);
        if (batch.Coverage != "declared_complete" || rows.Any(row => row.ValidationFindings.Count > 0))
            return Fail(RequestErrorKind.Conflict, "Only a valid declared-complete batch can propose retirement.");
        var missing = view.Missing(batch, rows);
        if (missing.Count < 1)
            return Fail(RequestErrorKind.Validation, "A retirement proposal requires at least one missing claim.");
        if (ApplicationImportOmissions.HasSharedOrPresentAlias(history, view, missing, rows))
            return Fail(RequestErrorKind.Conflict, "A source alias cannot authorize retirement of a shared target.");
        foreach (var claim in missing)
        {
            var target = await reader.HydrateApplicationAsync(request.TenantId, claim.Observation.ApplicationId, ct)
                .ConfigureAwait(false);
            if (!target.IsCreated || target.Id != claim.Observation.ApplicationId ||
                target.Stream.Realm != request.TenantId.ToString())
                return Fail(RequestErrorKind.NotFound, "The retirement target was not found.");
            if (target.IsRetired || target.Revision != (claim.Observation.ExpectedApplicationRevision ?? 1) ||
                target.CheckPendingImportChanges() is not null)
                return Fail(RequestErrorKind.Conflict, "The retirement target changed or has unsettled effects.");
        }

        var proposal = new ApplicationImportOmissionProposalContent(batch.SourceKey!, batch.SourceNamespace!,
            request.ExpectedSourcePosition, batch.ContentDigest!,
            Array.AsReadOnly(rows.Select(row => row.SourceRecordId!).ToArray()), memberId, display.Trim(),
            request.Reason.Trim(), now, Array.AsReadOnly(missing.Select(claim =>
                ApplicationImportOmissions.RetirementRow(view.Source.Id, claim)).ToArray()));
        return Result<ImmutableContentRef>.Success(await ApplicationImportContent.WriteAsync(content, request.TenantId,
            ApplicationImportOmissionProposalContent.Schema, proposal, ct).ConfigureAwait(false));
    }

    static Result<ImmutableContentRef> Fail(RequestErrorKind kind, string message, bool transient = false) =>
        Result<ImmutableContentRef>.Failure(new RequestError(kind, message, transient));
}
