using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Resumably creates the initial fixed platform seed as individual source facts.</summary>
/// <remarks>
/// The seed's literal entries are streamed twice: once to bind their exact
/// release fingerprint, and once to submit independent idempotent operations.
/// No edition or aggregate owns a manifest. This bootstrap sequencing ensures
/// the historical seed was submitted before its publication request; normal
/// edition publication itself does not wait for a child collection.
/// </remarks>
sealed class PlatformCriteriaSeedBootstrap(ICriteriaSeedWriter writer)
{
    public async ValueTask<CriteriaSeedBootstrapResult> RunAsync(CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ct.ThrowIfCancellationRequested();
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var fingerprint = CriteriaSeedFingerprint.Compute(edition, PlatformCriteriaCatalog.EnumerateSeedEntries());
        var header = new CriteriaSeedHeader(edition.EditionId, edition.Framework, edition.EditionLabel,
            edition.PublishedAt, edition.IsComplete, edition.CoverageNote, edition.SourceUrl,
            edition.ContentRights, CriteriaSeedFingerprint.Version, fingerprint);

        var binding = await writer.BindEditionAsync(header, Operation(header, "edition"), ct).ConfigureAwait(false);
        if (binding.Status != CriteriaSeedWriteStatus.Confirmed)
            return Stop(binding.Status, "edition");
        if (binding.EvidenceId == Uuid.Empty)
            throw new InvalidOperationException("A confirmed seed release requires its exact committed binding fact.");

        var metadata = await writer.EnsureMetadataAsync(header, binding.EvidenceId,
            Operation(header, "metadata"), ct).ConfigureAwait(false);
        if (metadata.Status != CriteriaSeedWriteStatus.Confirmed)
            return Stop(metadata.Status, "metadata");

        foreach (var gap in edition.SupportGaps)
        {
            ct.ThrowIfCancellationRequested();
            var step = "disclosure:" + gap.Category + ":" + gap.Code;
            var outcome = await writer.EnsureDisclosureAsync(header, binding.EvidenceId,
                Operation(header, step), gap, ct).ConfigureAwait(false);
            if (outcome.Status != CriteriaSeedWriteStatus.Confirmed)
                return Stop(outcome.Status, step);
        }

        string? witness = null;
        string? pendingStep = null;
        foreach (var entry in PlatformCriteriaCatalog.EnumerateSeedEntries())
        {
            ct.ThrowIfCancellationRequested();
            var step = "entry:" + entry.Identifier;
            var outcome = await writer.EnsureEntryAsync(header, binding.EvidenceId,
                Operation(header, step), entry, ct).ConfigureAwait(false);
            if (outcome.Status == CriteriaSeedWriteStatus.Conflict)
                return Stop(outcome.Status, step);
            if (outcome.Status == CriteriaSeedWriteStatus.Pending)
            {
                pendingStep ??= step;
                continue;
            }
            if (outcome.Status != CriteriaSeedWriteStatus.Confirmed)
                throw new InvalidOperationException("A seed entry returned an invalid status.");
            if (witness is null && entry.Kind == "criterion")
                witness = entry.Identifier;
        }

        // Independent entries may progress while another participant is pending.
        // Only one scalar pending step is retained; publication still waits for all.
        if (pendingStep is not null)
            return Stop(CriteriaSeedWriteStatus.Pending, pendingStep);

        if (witness is null)
            throw new InvalidOperationException("The platform seed has no criterion to witness publication.");
        var publication = await writer.PublishAsync(header, binding.EvidenceId,
            Operation(header, "publication"), witness, ct).ConfigureAwait(false);
        if (publication.Status != CriteriaSeedWriteStatus.Confirmed)
            return Stop(publication.Status, "publication");

        var coverage = await writer.ConfirmCoverageAsync(header, binding.EvidenceId, ct).ConfigureAwait(false);
        return coverage.Status == CriteriaSeedWriteStatus.Confirmed
            ? new CriteriaSeedBootstrapResult(CriteriaSeedWriteStatus.Confirmed, "complete")
            : Stop(coverage.Status, "coverage");
    }

    static Uuid Operation(CriteriaSeedHeader header, string step) =>
        PlatformCriteriaSeedDefinition.OperationOf(header.EditionId, step);

    static CriteriaSeedBootstrapResult Stop(CriteriaSeedWriteStatus status, string step) => status switch
    {
        CriteriaSeedWriteStatus.Pending or CriteriaSeedWriteStatus.Conflict => new(status, step),
        _ => throw new InvalidOperationException("A seed operation returned an invalid or unverified status."),
    };
}
