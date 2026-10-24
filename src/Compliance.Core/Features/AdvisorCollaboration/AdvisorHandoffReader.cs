using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     Reads a handoff together with its current selection. The selection is verified immutable content referenced by
///     the latest selection fact, so a missing or altered manifest fails closed instead of reading as an empty selection.
/// </summary>
public sealed class AdvisorHandoffReader(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>Hydrates one handoff and reads its selection.</summary>
    public async ValueTask<AdvisorHandoffSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid handoffId,
        CancellationToken ct = default) =>
        await ReadAsync(await reader.HydrateAsync(new AdvisorHandoff(tenantId, programId, handoffId), ct)
            .ConfigureAwait(false), tenantId, ct).ConfigureAwait(false);

    /// <summary>Reads the selection of an already hydrated handoff.</summary>
    public async ValueTask<AdvisorHandoffSnapshot> ReadAsync(AdvisorHandoff handoff, Uuid tenantId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        if (handoff.SelectionManifest is not { } manifest)
            return new AdvisorHandoffSnapshot(handoff, []);
        var selection = await AdvisorContent.ReadAsync<IReadOnlyList<AdvisorRecordVersion>>(content, tenantId,
            manifest, ct).ConfigureAwait(false);
        return new AdvisorHandoffSnapshot(handoff, selection);
    }
}
