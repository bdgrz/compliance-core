using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     Admits one exact sample selection on the open request for both the record and the amend command. The exact frozen
///     population version content and the nonempty duplicate-free row manifest are frozen on the request before the 202
///     acknowledgment, so the selection owner materializes from the accepted proof without reading the live request or
///     the latest population, even after the request resolved.
/// </summary>
sealed class SampleSelectionAdmission(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock)
{
    const string ClosedMessage = "A sample selection can only be recorded for an open request.";

    public async ValueTask<Result<OperationAcceptedResponse>> AdmitAsync(IExecutionContext context, Uuid actorMemberId,
        string actorDisplay, string commandKind, string digest, Uuid tenantId, Uuid programId, Uuid examinationId,
        Uuid requestId, long expectedRevision, Uuid selectionId, Uuid? supersedesSelectionId, Uuid populationId,
        int populationVersion, IReadOnlyList<Uuid> rowIds, string method, string source, string externalReference,
        string? amendmentReason, CancellationToken ct)
    {
        if (selectionId == Uuid.Empty || requestId == Uuid.Empty || expectedRevision < 0)
            return AuditorCommands.Validation<OperationAcceptedResponse>(
                "A sample selection requires its own identity and a non-negative expected request revision.");
        var now = clock.GetUtcNow();
        var snapshot = await reader.HydrateAsync(new AuditorRequest(tenantId, programId, requestId, selectionId), ct)
            .ConfigureAwait(false);
        var provenance = AuditorContent.Provenance(selectionId, commandKind, snapshot.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)expectedRevision, digest, now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        if (snapshot.Acceptance.Decide(binding) == NativeAcceptanceDecision.None)
        {
            var terms = await AuditorCommands.TermsAsync(content, tenantId, snapshot, ct).ConfigureAwait(false);
            if (!terms.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(terms.Error!);
            var prepared = snapshot.PrepareSelection(expectedRevision, selectionId, supersedesSelectionId, populationId,
                populationVersion);
            if (!prepared.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(prepared.Error!);
            if (terms.Value!.ExaminationId != examinationId)
                return Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The auditor request belongs to a different examination."));
            var selection = AuditorRequestRules.Selection(method, source, externalReference, amendmentReason,
                supersedesSelectionId is not null, rowIds);
            if (!selection.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(selection.Error!);
            var population = await SampleSelectionPopulationSourceReader.LoadAsync(reader, tenantId, programId,
                examinationId, populationId, populationVersion, selection.Value!.RowIds, ct).ConfigureAwait(false);
            if (!population.IsSuccess)
                return Result<OperationAcceptedResponse>.Failure(population.Error);
            var populationContent = await AuditorContent.WriteDeterministicAsync(content, tenantId, selectionId,
                AuditorContent.PopulationVersionSchema, population.Value, ct).ConfigureAwait(false);
            var rows = await ImmutableSelectionManifests.WriteAsync<Uuid>(content, tenantId.ToString(),
                Uuid.CreateVersion5(selectionId, AuditorContent.PopulationRowSchema), AuditorContent.PopulationRowSchema,
                selection.Value.RowIds, SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
            intent = await AuditorContent.WriteAsync(content, tenantId, AuditorSampleSelectionIntent.Schema,
                new AuditorSampleSelectionIntent(AuditorContent.Header(provenance, now), programId, requestId,
                    (ulong)snapshot.Generation, (ulong)expectedRevision + 1, selectionId, supersedesSelectionId,
                    populationId, populationVersion, populationContent, rows, selection.Value.Method,
                    selection.Value.Source, selection.Value.ExternalReference, selection.Value.AmendmentReason, now), ct)
                .ConfigureAwait(false);
            response = await AuditorContent.AcceptedResponseAsync(content, tenantId, selectionId, now, ct)
                .ConfigureAwait(false);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(tenantId, programId, requestId, selectionId),
            auditorRequest => auditorRequest.Acceptance,
            auditorRequest => AuditorContent.Prepared(auditorRequest.PrepareSelection(expectedRevision, selectionId,
                supersedesSelectionId, populationId, populationVersion), response),
            (AuditorRequest auditorRequest, ImmutableContentRef acknowledgment) =>
                auditorRequest.AdmitSelection(provenance, intent!, populationId, populationVersion,
                    supersedesSelectionId, acknowledgment)), context, ct).ConfigureAwait(false);
        return await AuditorContent.ResponseAsync<OperationAcceptedResponse>(content, tenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
