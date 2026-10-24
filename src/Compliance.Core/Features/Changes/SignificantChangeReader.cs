using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Reconstructs exact verified business content at a native owner cut or recorded-through receipt cut.</summary>
public sealed class SignificantChangeReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async ValueTask<SignificantChangeReadSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid changeId,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new SignificantChange(tenantId, programId, changeId), ct).ConfigureAwait(false);
        return await ReadAsync(owner, programId, null, ct).ConfigureAwait(false);
    }
    public async ValueTask<SignificantChangeRules> RulesAsync(Uuid tenantId, Uuid programId, Uuid changeId, CancellationToken ct = default) =>
        (await ReadAsync(tenantId, programId, changeId, ct).ConfigureAwait(false)).Rules;
    public async ValueTask<SignificantChangeReadSnapshot> ReadThroughAsync(Uuid tenantId, Uuid programId, Uuid changeId,
        DateTimeOffset recordedThrough, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new SignificantChange(tenantId, programId, changeId), ct).ConfigureAwait(false);
        return await ReadAsync(owner, programId, recordedThrough, ct).ConfigureAwait(false);
    }
    internal ValueTask<SignificantChangeReadSnapshot> ReadAsync(SignificantChange owner, Uuid programId, CancellationToken ct) =>
        ReadAsync(owner, programId, null, ct);
    async ValueTask<SignificantChangeReadSnapshot> ReadAsync(SignificantChange owner, Uuid programId,
        DateTimeOffset? through, CancellationToken ct)
    {
        var rules = new SignificantChangeRules(owner.TenantId, programId, owner.Id);
        if (owner.Header is null || owner.ProgramId != programId)
            return new(owner, rules);
        var sequence = 0L;
        ISignificantChangeFact? last = null;
        DateTimeOffset? previousReceipt = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(owner.TenantId.ToString(),
            SignificantChange.Area, owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not ISignificantChangeFact fact || fact.NativeSequence != sequence + 1 ||
                record.Stream != owner.Stream || fact.Header.ProgramId != programId ||
                previousReceipt is { } receipt && fact.Operation.AcceptedAt < receipt)
                throw new ImmutableContentIntegrityException("The change history has another native sequence, scope or receipt order.");
            if (fact.NativeSequence > owner.NativeSequence || through is { } cutoff && fact.Operation.AcceptedAt > cutoff)
                break;
            var mutation = await content.ReadJsonAsync<SignificantChangeMutation>(owner.TenantId.ToString(), fact.Change, ct).ConfigureAwait(false);
            var body = await content.ReadJsonAsync<SignificantChangeView>(owner.TenantId.ToString(), fact.CurrentContent, ct).ConfigureAwait(false);
            if (fact.Change.Schema != SignificantChangeContentStore.ChangeSchema || fact.CurrentContent.Schema != SignificantChangeContentStore.BodySchema ||
                mutation.Kind != fact.Operation.CommandKind || fact.ChangeCount != (mutation.Changed ? 1 : 0) ||
                !SignificantChangeContentStore.ValidMutation(rules.Current, body, mutation) ||
                SignificantChangeContentStore.Header(body) != fact.Header)
                throw new ImmutableContentIntegrityException("The change immutable body differs from its native transition or authority.");
            SignificantChangeContentStore.Apply(rules, mutation);
            sequence = fact.NativeSequence;
            previousReceipt = fact.Operation.AcceptedAt;
            last = fact;
        }
        if (through is null && (sequence != owner.NativeSequence || last is null || last.Header != owner.Header || last.CurrentContent != owner.CurrentContent))
            throw new ImmutableContentUnavailableException("The complete exact native change cut is not readable yet.");
        return new(owner, rules);
    }
}
