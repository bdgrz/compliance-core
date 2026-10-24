using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>
///     The reservation component a source owner embeds. The owner's reducers hand every grant and settlement fact to
///     it, so the active grants, the monotonic <see cref="Generation" /> and the fences are rebuilt from the ordered
///     stream. A writer of the owner asks <see cref="HasActiveUse" /> before it changes the held source. A settlement
///     before any grant leaves a fence, so a late acquisition of the same key is refused; no lifetime fence dictionary
///     exists beyond the slots of the owner's own stream.
/// </summary>
public sealed class SourceUseReservations(EventStreamAddress owner)
{
    readonly Dictionary<Slot, SourceUseEntry> _entries = [];

    /// <summary>The receiver's own monotonic grant counter; the last issued <c>ReservationGeneration</c>.</summary>
    public ulong Generation { get; private set; }

    /// <summary>True while any granted use is not settled; a conflicting writer must wait.</summary>
    public bool HasActiveUse => _entries.Values.Any(entry => entry.IsActive);

    /// <summary>The grants that are granted and not settled, in grant order.</summary>
    public IReadOnlyList<SourceUseEntry> ActiveUses => Array.AsReadOnly(_entries.Values
        .Where(entry => entry.IsActive).OrderBy(entry => entry.ReservationGeneration).ToArray());

    /// <summary>The slot of a key on this owner, or null when the key was never granted or fenced.</summary>
    public SourceUseEntry? Find(ReservationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Target != owner)
            throw new ArgumentException("The key names another target owner.", nameof(key));
        return _entries.GetValueOrDefault(Slot.Of(key));
    }

    /// <summary>The grant token of a key that was granted, whether or not it was settled since.</summary>
    public ReservationGrant? GrantOf(ReservationKey key) => Find(key) is { GrantFact: not null } entry
        ? new ReservationGrant(key, entry.ReservationGeneration, NativeFactReference.Of(owner, entry.GrantFact))
        : null;

    /// <summary>Reducer hook: a grant fact of <paramref name="purpose" /> was appended to the owner.</summary>
    public void ApplyGranted(SourceUsePurpose purpose, ISourceUseFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var slot = SlotOf(purpose, fact);
        if (_entries.ContainsKey(slot))
            throw new InvalidOperationException("A source use is granted at most once and never after its fence.");
        Generation++;
        _entries.Add(slot, new SourceUseEntry(Generation, (DomainEvent)fact, null));
    }

    /// <summary>Reducer hook: a settlement fact of <paramref name="purpose" /> was appended to the owner.</summary>
    public void ApplySettled(SourceUsePurpose purpose, ISourceUseFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var slot = SlotOf(purpose, fact);
        if (_entries.TryGetValue(slot, out var entry))
        {
            if (entry.SettleFact is not null)
                throw new InvalidOperationException("A source use is settled at most once.");
            _entries[slot] = entry with { SettleFact = (DomainEvent)fact };
            return;
        }

        _entries.Add(slot, new SourceUseEntry(0, null, (DomainEvent)fact));
    }

    /// <summary>The wire name of a purpose, as carried by <see cref="ReservationKey.Purpose" />.</summary>
    public static string PurposeName(SourceUsePurpose purpose)
    {
        var name = purpose.ToString();
        var wire = new System.Text.StringBuilder(name.Length + 4);
        foreach (var character in name)
        {
            if (char.IsUpper(character) && wire.Length > 0)
                wire.Append('_');
            wire.Append(char.ToLowerInvariant(character));
        }

        return wire.ToString();
    }

    Slot SlotOf(SourceUsePurpose purpose, ISourceUseFact fact)
    {
        if (fact.Owner != owner || fact.Operation.Owner != owner || fact.Generation < 0 ||
            fact.Operation.RootOperationId == Uuid.Empty)
            throw new InvalidOperationException("A source use fact belongs to its own owner stream and root operation.");
        return new Slot(fact.Operation.RootOperationId, fact.Operation.FirstOwner ?? fact.Operation.Owner,
            (ulong)fact.Generation, PurposeName(purpose));
    }

    readonly record struct Slot(Uuid Root, EventStreamAddress FirstOwner, ulong SourceGeneration, string Purpose)
    {
        public static Slot Of(ReservationKey key) =>
            new(key.RootOperationId, key.FirstOwner, key.SourceGeneration, key.Purpose);
    }
}
