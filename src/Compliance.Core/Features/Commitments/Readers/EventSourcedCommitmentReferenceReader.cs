using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments.Readers;

sealed class EventSourcedCommitmentReferenceReader(IAggregateReader reader)
    : ICommitmentReferenceReader
{
    public async ValueTask<bool> IsRecordedAsync(Uuid tenantId, Uuid draftId,
        CancellationToken ct = default) =>
        (await reader.HydrateAsync(new CommitmentDraft(tenantId, draftId), ct)
            .ConfigureAwait(false)).IsCreated;
}
