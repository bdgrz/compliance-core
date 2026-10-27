using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

sealed class EvidenceRedactionRead(EvidenceArtifactReadAccess access, EvidenceRedactionSources sources, IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content,
    ISourceFactResolver resolver)
{
    public async ValueTask<Result<EvidenceRedactionReadSnapshot>> GetAsync(IRequestContext context, Uuid tenantId, Uuid redactionId, CancellationToken ct)
    {
        try
        {
            return await GetCoreAsync(context, tenantId, redactionId, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<EvidenceRedactionReadSnapshot>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact retained redaction lineage cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<EvidenceRedactionReadSnapshot>> GetCoreAsync(IRequestContext context, Uuid tenantId, Uuid redactionId, CancellationToken ct)
    {
        var allowed = await access.RequireMembershipAsync(context, tenantId, ct).ConfigureAwait(false);
        if (!allowed.IsSuccess)
            return Result<EvidenceRedactionReadSnapshot>.Failure(allowed.Error);
        var redaction = await reader.HydrateAsync(new EvidenceRedaction(tenantId, redactionId), ct).ConfigureAwait(false);
        var cut = EvidenceRedactionPreparedCut.Of(redaction);
        var history = await EvidenceRedactionHistory.ReadAsync(events, redaction, content, resolver, ct).ConfigureAwait(false);
        if (redaction.CurrentPreparation is not { } preparation)
            return Result<EvidenceRedactionReadSnapshot>.Failure(new RequestError(RequestErrorKind.NotFound, "The redaction lineage was not found."));
        var captured = await sources.CaptureAsync(context, tenantId, preparation.Original.ArtifactId, preparation.Derived.ArtifactId, ct).ConfigureAwait(false);
        if (!captured.IsSuccess)
            return Result<EvidenceRedactionReadSnapshot>.Failure(captured.Error.Kind == RequestErrorKind.NotFound ?
                new RequestError(RequestErrorKind.NotFound, "The redaction lineage was not found.") : captured.Error);
        if (EvidenceRedactionSources.Identity(captured.Value.Original) != preparation.Original ||
            EvidenceRedactionSources.Identity(captured.Value.Derived) != preparation.Derived)
            return Result<EvidenceRedactionReadSnapshot>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The current artifacts differ from the retained preparation identities."));
        // Applicability retains the exact observed source cut and availability fact, including older approvals.
        // This check grants no future source hold or disclosure authority.
        if (!HasConsistentApprovalSources(history, captured.Value))
            return Result<EvidenceRedactionReadSnapshot>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The retained approval source proof differs from the observed derived artifact."));
        var checkedSources = await sources.CheckAsync(context, captured.Value, ct).ConfigureAwait(false);
        if (!checkedSources.IsSuccess)
            return Result<EvidenceRedactionReadSnapshot>.Failure(checkedSources.Error);
        var current = await reader.HydrateAsync(new EvidenceRedaction(tenantId, redactionId), ct).ConfigureAwait(false);
        if (EvidenceRedactionPreparedCut.Of(current) != cut)
            return Result<EvidenceRedactionReadSnapshot>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The redaction lineage changed during this request.", isTransient: true));
        return Result<EvidenceRedactionReadSnapshot>.Success(new(redaction, captured.Value, history));
    }

    internal static bool HasConsistentApprovalSources(EvidenceRedactionHistorySnapshot history, EvidenceRedactionSourcePair sources) =>
        history.Approvals.All(approval => approval.DerivedSourcePosition == sources.Derived.Metadata.SourcePosition &&
            approval.DerivedAvailableAt == sources.Derived.AvailabilityRecordedAt);

    internal static EvidenceRedactionView View(EvidenceRedactionReadSnapshot snapshot) => snapshot.History.View(
        snapshot.Sources.Original.Metadata.State, snapshot.Sources.Derived.Metadata.State,
        snapshot.Redaction.CurrentApproval is not null && snapshot.Sources.Derived.Metadata.State == EvidenceArtifactStates.Available);
}
