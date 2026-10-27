using Bdgrz.Compliance.Features.Examinations.Models;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Request authority and earlier source references available to one family reader.</summary>
public sealed record TypeIBaselineSourceReadContext(Uuid RequesterUserId,
    IReadOnlyList<TypeIBaselineSourceReference> PreviouslyCapturedSources)
{
    /// <summary>Optional server-frozen receipt cutoff for a close; the engagement date remains the effective cutoff.</summary>
    public DateTimeOffset? RecordedThrough { get; init; }
    public RecordedSourceReceiptCut? ReceiptCut { get; init; }
    public RecordedSourceReceiptCut RequireReceipts(Uuid tenant)
    {
        if (RecordedThrough is not { } through || ReceiptCut is not { } cut)
            throw new ImmutableContentUnavailableException("The exact closing source broker receipt selection is not configured.");
        if (cut.TenantId != tenant || cut.RecordedThrough != through)
            throw new ImmutableContentIntegrityException("The closing source broker receipt selection has another exact tenant or cutoff.");
        return cut;
    }
    public ValueTask<bool> IncludesAsync(EventStreamAddress stream, DomainEvent fact, CancellationToken ct) => RecordedThrough is null
        ? ValueTask.FromResult(true) : RequireReceipts(ReceiptCut?.TenantId ?? Uuid.Empty).IncludesAsync(stream, fact, ct);
}
