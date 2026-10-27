using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Source-backed exact Control reconstruction. Only compact native facts are folded into command state.</summary>
public sealed class ControlReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async ValueTask<ControlReadSnapshot> ReadAsync(Uuid tenantId, Uuid controlId, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ControlDraft(tenantId, controlId), ct).ConfigureAwait(false);
        return await ReadAsync(owner, owner.ProgramId, ct).ConfigureAwait(false);
    }
    public async ValueTask<ControlReadSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new ControlDraft(tenantId, controlId), ct).ConfigureAwait(false);
        return await ReadAsync(owner, programId, ct).ConfigureAwait(false);
    }
    public async ValueTask<ControlRules> RulesAsync(Uuid tenantId, Uuid controlId, CancellationToken ct = default) =>
        (await ReadAsync(tenantId, controlId, ct).ConfigureAwait(false)).Rules;
    public async ValueTask<ControlRules> RulesAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        CancellationToken ct = default) => (await ReadAsync(tenantId, programId, controlId, ct).ConfigureAwait(false)).Rules;

    public async ValueTask<ControlDraftRevisionView?> RevisionAsync(Uuid tenantId, Uuid programId, Uuid controlId,
        long revision, CancellationToken ct = default)
    {
        var snapshot = await ReadAsync(tenantId, programId, controlId, ct).ConfigureAwait(false);
        if (!snapshot.IsVisible)
            return null;
        foreach (var change in snapshot.Rules.AppliedChanges)
        {
            if (change is ControlDraftCreatedContent created && revision == 1)
                return new ControlDraftRevisionView(tenantId, programId, controlId, snapshot.Rules.Identifier, 1,
                    created.Content, created.ActorMemberId, created.ActorDisplay, created.ChangedAt)
                { Actor = created.Actor };
            if (change is ControlDraftRevisedContent revised && revised.Revision == revision)
                return new ControlDraftRevisionView(tenantId, programId, controlId, snapshot.Rules.Identifier, revision,
                    revised.Content, revised.ActorMemberId, revised.ActorDisplay, revised.ChangedAt)
                { Actor = revised.Actor };
        }
        return null;
    }

    internal async ValueTask<ControlReadSnapshot> ReadAsync(ControlDraft owner, Uuid programId, CancellationToken ct)
    {
        var rules = new ControlRules(owner.TenantId, owner.Id);
        if (!owner.IsCreated || owner.ProgramId != programId)
            return new ControlReadSnapshot(owner, rules, null, [], false);
        var retirements = new List<ControlRetired>();
        var sequence = 0L;
        IControlFact? last = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(owner.TenantId.ToString(),
            ControlDraft.Area, owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not IControlFact fact || fact.NativeSequence != sequence + 1 ||
                fact.NativeSequence > owner.NativeSequence)
                throw new ImmutableContentIntegrityException("The control source has another native authority sequence.");
            var mutation = await ControlFactContent.ReadChangeAsync(content, fact, ct).ConfigureAwait(false);
            foreach (var change in mutation.Changes)
                rules.Apply(change);
            var current = await ControlFactContent.ReadDraftAsync(content, fact, ct).ConfigureAwait(false);
            if (rules.CurrentContent is null || ContentDigest.OfJson(current) != ContentDigest.OfJson(rules.CurrentContent) ||
                rules.Header(fact.Header.Content) != fact.Header || fact.Authority.Schema != ControlFactContent.AuthoritySchema)
                throw new ImmutableContentIntegrityException("The control content and compact native authority differ.");
            var authority = await content.ReadJsonAsync<ControlAuthorityContent>(owner.TenantId.ToString(), fact.Authority, ct)
                .ConfigureAwait(false);
            if (ContentDigest.OfJson(authority) != ContentDigest.OfJson(rules.Authority(fact.Header)))
                throw new ImmutableContentIntegrityException("The control current-scope responsibility/decision authority differs.");
            if (record.Event is ControlRetired retired && fact.ChangeCount > 0)
                retirements.Add(retired);
            last = fact;
            sequence = fact.NativeSequence;
            if (sequence == owner.NativeSequence)
                break;
        }
        if (sequence != owner.NativeSequence || last is null)
            throw new ImmutableContentUnavailableException("The exact control authority source cut is not visible yet.");
        if (last.Authority != owner.CurrentAuthority || last.Header != owner.Header)
            throw new ImmutableContentIntegrityException("The reconstructed control differs from its hydrated exact owner cut.");
        rules.ClearPendingChanges();
        rules.SourcePhysicalPosition = owner.CommittedStreamPosition;
        return new ControlReadSnapshot(owner, rules, rules.DraftView(), retirements.AsReadOnly());
    }
}
