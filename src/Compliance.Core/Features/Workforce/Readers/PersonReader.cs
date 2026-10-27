using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Verified current or exact historical producing values; never a projection or live replacement for a source version.</summary>
public sealed class PersonReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async Task<PersonReadSnapshot> ReadAsync(Uuid tenant, Uuid id, CancellationToken ct = default) =>
        await ReadAsync(await reader.HydrateAsync(new Person(tenant, id), ct).ConfigureAwait(false), ct).ConfigureAwait(false);
    public async Task<PersonReadSnapshot> ReadAsync(Person owner, CancellationToken ct = default)
    {
        var facts = await PersonHistory.ReadAsync(owner, events, ct).ConfigureAwait(false);
        if (!owner.IsCreated)
            return new(owner, null);
        var view = facts[^1] is IPersonNativeFact ? await ReadNativeAsync(facts, facts[^1], ct).ConfigureAwait(false) : Legacy(owner, facts);
        if (view is null || PersonContent.Header(view) != owner.Header)
            throw new ImmutableContentIntegrityException("The current person values differ from their exact native header.");
        return new(owner, view);
    }
    public async Task<PersonView> ReadFactAsync(DomainEvent fact, CancellationToken ct = default)
    {
        if (fact is not IPersonNativeFact native)
            throw new ImmutableContentIntegrityException("The source is not a native person fact.");
        var owner = await reader.HydrateAsync(new Person(native.Header.TenantId, native.Header.PersonId, native.Operation.LogicalId), ct).ConfigureAwait(false);
        var facts = await PersonHistory.ReadAsync(owner, events, ct).ConfigureAwait(false);
        var actual = facts.FirstOrDefault(value => value.Metadata.EventId == fact.Metadata.EventId);
        if (actual is null)
            throw new ImmutableContentUnavailableException("The exact producing person fact is not visible.");
        if (!actual.Equals(fact) || actual.Metadata != fact.Metadata)
            throw new ImmutableContentIntegrityException("The person producing source differs from actual payload/full metadata.");
        return await ReadNativeAsync(facts, actual, ct).ConfigureAwait(false);
    }
    async Task<PersonView> ReadNativeAsync(IReadOnlyList<DomainEvent> facts, DomainEvent fact, CancellationToken ct)
    {
        var native = (IPersonNativeFact)fact;
        var realm = native.Header.TenantId.ToString();
        var view = await content.ReadJsonAsync<PersonView>(realm, native.Content, ct).ConfigureAwait(false);
        var mutation = await content.ReadJsonAsync<PersonChangeContent>(realm, native.Change, ct).ConfigureAwait(false);
        var index = facts.ToList().IndexOf(fact);
        PersonHeader? before = null;
        Bdgrz.Compliance.Features.Proofs.ImmutableContentRef? prior = null;
        foreach (var value in facts.Take(index))
        {
            if (value is IPersonNativeFact previous)
            { before = previous.Header; prior = previous.Content; }
            else
                before = PersonHistory.NextLegacy(native.Header.TenantId, native.Header.PersonId, before, value);
        }
        if (mutation.Before != before || mutation.BeforeContent != prior || mutation.After != native.Header || mutation.Content != native.Content ||
            mutation.CommandKind != native.Operation.CommandKind || mutation.Changed != native.Changed || PersonContent.Header(view) != native.Header ||
            view.SourceKind != "manual" || view.RestrictedFieldsRedacted)
            throw new ImmutableContentIntegrityException("The immutable person body/mutation differs from its exact producing transition.");
        return view;
    }
    static PersonView? Legacy(Person owner, IReadOnlyList<DomainEvent> facts)
    {
        var rules = new PersonRules(owner.TenantId, owner.Id);
        foreach (var fact in facts)
            rules.Observe(fact);
        return rules.Current;
    }
}
