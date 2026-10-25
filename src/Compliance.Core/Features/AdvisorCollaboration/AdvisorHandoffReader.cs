using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Composes exact immutable terms, selection and native history outside the handoff command owner.</summary>
public sealed class AdvisorHandoffReader(IAggregateReader reader, IImmutableContentStore content,
    IDomainEventReader events)
{
    public async ValueTask<AdvisorHandoffSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid handoffId,
        CancellationToken ct = default) =>
        await ReadAsync(await reader.HydrateAsync(new AdvisorHandoff(tenantId, programId, handoffId), ct)
            .ConfigureAwait(false), tenantId, ct).ConfigureAwait(false);

    public async ValueTask<AdvisorHandoffSnapshot> ReadAsync(AdvisorHandoff handoff, Uuid tenantId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        if (handoff.TenantId != tenantId)
            throw new ImmutableContentIntegrityException("The handoff content realm differs from its native owner.");
        var facts = await AdvisorNativeHistory.ReadAsync(handoff, events, ct).ConfigureAwait(false);
        if (handoff.CreatedFact is not { } created)
            return new AdvisorHandoffSnapshot(handoff, [], null, facts);
        var original = await AdvisorAuthored.ReadAsync(content, created, ct).ConfigureAwait(false);
        var expected = original.RecordVersions;
        var current = facts.Last(static value => value is AdvisorHandoffCreated or AdvisorHandoffSelectionRevised);
        if (current is AdvisorHandoffSelectionRevised revised)
            expected = (await AdvisorAuthored.ReadAsync(content, revised, ct).ConfigureAwait(false)).RecordVersions;
        var selection = await AdvisorContent.ReadAsync<IReadOnlyList<AdvisorRecordVersion>>(content, tenantId,
            handoff.SelectionManifest!, ct).ConfigureAwait(false);
        if (!AdvisorHandoffRules.ValidSelection(selection) || !selection.SequenceEqual(expected))
            throw new ImmutableContentIntegrityException("The current selection differs from its exact native authored request.");
        return new AdvisorHandoffSnapshot(handoff, selection, original.Purpose, facts);
    }

    internal async ValueTask<DomainEvent?> FindFactAsync(AdvisorHandoff handoff, Uuid eventId, CancellationToken ct)
    {
        var facts = await AdvisorNativeHistory.ReadAsync(handoff, events, ct).ConfigureAwait(false);
        return facts.SingleOrDefault(value => value.Metadata.EventId == eventId);
    }
}
