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
    IDomainEventReader events,
    IImmutableContentStore content, TimeProvider clock)
{
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
        var address = new AuditorRequest(tenantId, programId, requestId, selectionId);
        var provenance = AuditorContent.Provenance(selectionId, commandKind, address.Stream,
            AuditorCommands.Actor(actorMemberId, actorDisplay), (ulong)expectedRevision, digest, now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        FirstResponseDescriptor? response = null;
        AuditorRequestHistorySnapshot? history = null;

        var outcome = await operations.ExecuteAsync(new NativeOperation<AuditorRequest>(binding,
            () => new AuditorRequest(tenantId, programId, requestId, selectionId),
            auditorRequest => auditorRequest.Acceptance,
            async (snapshot, prepareCt) =>
            {
                history = await AuditorRequestHistory.ReadAsync(events, snapshot, prepareCt).ConfigureAwait(false);
                var terms = await AuditorCommands.TermsAsync(content, tenantId, snapshot, prepareCt).ConfigureAwait(false);
                if (!terms.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(terms.Error!);
                var prepared = snapshot.PrepareSelection(expectedRevision, selectionId, supersedesSelectionId, populationId,
                populationVersion, history);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                if (terms.Value!.ExaminationId != examinationId)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The auditor request belongs to a different examination."));
                var selection = AuditorRequestRules.Selection(method, source, externalReference, amendmentReason,
                supersedesSelectionId is not null, rowIds);
                if (!selection.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(selection.Error!);
                var population = await SampleSelectionPopulationSourceReader.LoadAsync(reader, tenantId, programId,
                examinationId, populationId, populationVersion, selection.Value!.RowIds, prepareCt).ConfigureAwait(false);
                if (!population.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(population.Error);
                var populationContent = await AuditorContent.WriteAsync(content, tenantId,
                AuditorContent.PopulationVersionSchema, population.Value, prepareCt).ConfigureAwait(false);
                var rows = await ImmutableSelectionManifests.WriteAsync<Uuid>(content, tenantId.ToString(),
                Uuid.CreateVersion4(), AuditorContent.PopulationRowSchema,
                selection.Value.RowIds, SelectionOrder.CanonicalKey, prepareCt).ConfigureAwait(false);
                intent = await AuditorContent.WriteAsync(content, tenantId, AuditorSampleSelectionIntent.Schema,
                new AuditorSampleSelectionIntent(AuditorContent.Header(provenance, now), programId, requestId,
                    (ulong)snapshot.Generation, (ulong)expectedRevision + 1, selectionId, supersedesSelectionId,
                    populationId, populationVersion, populationContent, rows, selection.Value.Method,
                    selection.Value.Source, selection.Value.ExternalReference, selection.Value.AmendmentReason, now), prepareCt)
                .ConfigureAwait(false);
                response = await AuditorContent.AcceptedResponseAsync(content, tenantId, selectionId, now, prepareCt)
                .ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(response!);
            },
            (AuditorRequest auditorRequest, ImmutableContentRef acknowledgment) =>
                auditorRequest.AdmitSelection(provenance, intent!, populationId, populationVersion,
                    supersedesSelectionId, acknowledgment, history!)), context, ct).ConfigureAwait(false);
        return await AuditorContent.ResponseAsync<OperationAcceptedResponse>(content, tenantId, outcome, ct)
            .ConfigureAwait(false);
    }
}
