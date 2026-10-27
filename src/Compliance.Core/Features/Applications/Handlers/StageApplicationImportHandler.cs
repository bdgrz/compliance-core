using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

/// <summary>
///     Stages one immutable observation as the root native acceptance of its submission on the new batch. The
///     complete rows are frozen as immutable content before the batch is touched, so the observation has no row or
///     frame cap; an exact retry replays the original registration and a changed observation conflicts.
/// </summary>
public sealed class StageApplicationImportHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<StageApplicationImport, ApplicationImportRegistration>
{
    const string CommandKind = "StageApplicationImport";
    const string InitialEpisode = "initial";

    public async ValueTask<Result<ApplicationImportRegistration>> HandleAsync(
        IRequestContext<StageApplicationImport> context, CancellationToken ct)
    {
        try
        {
            return await HandleCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<ApplicationImportRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The exact import batch source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<ApplicationImportRegistration>> HandleCoreAsync(IRequestContext<StageApplicationImport> context, CancellationToken ct)
    {
        var request = context.Request;
        var (memberId, display) = ApplicationActor.From(context);
        var batchId = request.TenantId != Uuid.Empty && request.SourceKey is not null && request.SourceNamespace is not null
            ? ApplicationImportBatch.BatchIdFor(request)
            : Uuid.Empty;
        if (batchId == Uuid.Empty)
            return Invalid();
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new ApplicationImportBatch(request.TenantId, batchId,
            request.SubmissionId), ct).ConfigureAwait(false);
        var provenance = new OperationProvenance(request.SubmissionId, request.SubmissionId, null, CommandKind,
            ActorReference.ForMember(memberId, display), snapshot.Stream, InitialEpisode, null,
            AuthoredIntentDigest.Compute(request), now, snapshot.Stream, snapshot.Stream, snapshot.Stream);
        var binding = AcceptedOperationBinding.Of(provenance);

        // A submission that was already accepted resolves from the owner's facts before any guard is evaluated.
        FirstResponseDescriptor? response = null;
        ImmutableContentRef? rows = null;
        ImmutableContentRef? result = null;
        string? digest = null;
        var invalid = 0;
        ApplicationImportBatchPreparedCut? preparedCut = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<ApplicationImportBatch>(binding,
            () => new ApplicationImportBatch(request.TenantId, batchId, request.SubmissionId),
            batch => batch.Acceptance,
            async (batch, prepareCt) =>
            {
                var prepared = batch.PrepareStage();
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                if (!ApplicationImportRowRules.ValidRequest(request))
                    return Result<FirstResponseDescriptor>.Failure(Invalid().Error!);
                var duplicated = ApplicationImportRowRules.Duplicated(request.Rows);
                var staged = request.Rows.Select((row, index) => new ApplicationImportStagedRow(
                    ApplicationImportEffects.RowId(batchId, index + 1), index + 1, row.SourceRecordId, row.Name,
                    row.Purpose, row.OwnerReference, ApplicationImportRowRules.Findings(row, duplicated))).ToArray();
                var manifest = new ApplicationImportRowsManifest(staged);
                if (ApplicationImportContent.Size(manifest) > ApplicationImportBatch.MaximumStagedBytes)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                        "The import observation exceeds the bounded immutable content size."));
                digest = ApplicationImportBatch.ObservationSha256(request);
                invalid = staged.Count(row => row.ValidationFindings.Count > 0);
                rows = await ApplicationImportContent.WriteAsync(content, request.TenantId,
                    ApplicationImportRowsManifest.Schema, manifest, prepareCt).ConfigureAwait(false);
                var registration = await ApplicationImportContent.WriteAsync(content, request.TenantId,
                    ApplicationImportContent.RegistrationSchema, new ApplicationImportRegistration(batchId, 1, digest), prepareCt)
                    .ConfigureAwait(false);
                result = await ApplicationImportContent.WriteResultAsync(content, request.TenantId,
                    ApplicationImportContent.RegistrationSchema, registration, prepareCt).ConfigureAwait(false);
                response = ApplicationImportContent.OkResponse(ApplicationImportContent.RegistrationSchema, registration);

                preparedCut = ApplicationImportBatchPreparedCut.Capture(batch);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (ApplicationImportBatch batch, ImmutableContentRef frozen) =>
            {
                if (preparedCut?.Matches(batch) != true)
                    return ApplicationImportBatchPreparedCut.Changed();
                return batch.Stage(provenance,
                request.SubmissionId, request.SourceKey!, request.SourceNamespace!, request.Coverage, digest!, rows!,
                request.Rows.Count, invalid, memberId, display, now, frozen, result!);
            }), context, ct)
            .ConfigureAwait(false);
        return await ApplicationImportContent.ResponseAsync<ApplicationImportRegistration>(content, request.TenantId,
            outcome, ct).ConfigureAwait(false);
    }

    static Result<ApplicationImportRegistration> Invalid() => Result<ApplicationImportRegistration>.Failure(
        new RequestError(RequestErrorKind.Validation,
            "An import requires a source, coverage, submission ID, and at least one bounded row."));
}
