using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>
/// Current live grants and explicitly selected historical slots. The verified aggregate reader validates the full
/// ordered history transiently before returning an existing owner. Closed slots never accumulate in command state.
/// </summary>
public sealed class SourceUseReservations(EventStreamAddress owner)
{
    readonly Dictionary<SourceUseSlot, SourceUseEntry> _active = [];
    readonly Dictionary<SourceUseSlot, SourceUseEntry> _selected = [];
    readonly HashSet<SourceUseSlot> _selection = [];
    bool _observed;
    bool _verified;
    ulong _verifiedVersion;
    ulong _verifiedPosition;
    string _historyDigest = SourceUseHistoryDigest.Empty;
    ulong _ownerFacts;

    /// <summary>The receiver's monotonic grant counter, independent of owner revisions and physical position.</summary>
    public ulong Generation { get; private set; }

    /// <summary>True while any granted use is not settled; a conflicting writer must wait.</summary>
    public bool HasActiveUse => _active.Count != 0;

    /// <summary>The live grants in receiver grant order.</summary>
    public IReadOnlyList<SourceUseEntry> ActiveUses => Array.AsReadOnly(_active.Values
        .OrderBy(entry => entry.ReservationGeneration).ToArray());

    /// <summary>Selects an exact historical key before replay; selection is request-local, never lifetime state.</summary>
    public void Select(ReservationKey key)
    {
        if (_observed || _ownerFacts != 0)
            throw new InvalidOperationException("Select historical source uses before hydrating the owner.");
        _selection.Add(SourceUseSlot.Of(owner, key));
    }

    /// <summary>A live or explicitly selected historical slot. Only an empty owner or a verified selection proves absence.</summary>
    public SourceUseEntry? Find(ReservationKey key)
    {
        var slot = SourceUseSlot.Of(owner, key);
        if (_active.TryGetValue(slot, out var active))
            return active;
        if (_selection.Contains(slot))
            return _selected.GetValueOrDefault(slot);
        if (!_observed && _ownerFacts == 0)
            return null;
        throw new InvalidOperationException("Historical source-use lookup requires explicit selection before hydration.");
    }

    /// <summary>The actual original grant of a selected or live key, including after settlement.</summary>
    public ReservationGrant? GrantOf(ReservationKey key) => Find(key) is { GrantFact: not null } entry
        ? new ReservationGrant(key, entry.ReservationGeneration, NativeFactReference.Of(owner, entry.GrantFact))
        : null;

    /// <summary>Requires the exact selected history proof at this owner's current append cut.</summary>
    public void RequireForAppend(SourceUsePurpose purpose, OperationProvenance operation, long generation,
        ulong ownerVersion, ulong physicalPosition)
    {
        var slot = SourceUseSlot.Of(owner, purpose, operation, generation);
        if (ownerVersion == 0 && physicalPosition == 0 && !_observed)
        {
            _selection.Add(slot);
            return;
        }
        if (!_verified || _verifiedVersion != ownerVersion || _verifiedPosition != physicalPosition ||
            !_selection.Contains(slot))
            throw new InvalidOperationException("A source-use append requires its finalized exact selected owner cut.");
    }

    /// <summary>Reducer hook for a grant. Full historical duplicate validation is performed at reader finalization.</summary>
    public void ApplyGranted(SourceUsePurpose purpose, ISourceUseFact fact)
    {
        var slot = SourceUseSlot.Of(owner, purpose, fact);
        if (_active.ContainsKey(slot) || _selected.ContainsKey(slot))
            throw new InvalidOperationException("A source use is granted at most once and never after its fence.");
        _observed = true;
        _verified = false;
        Generation = checked(Generation + 1);
        var entry = new SourceUseEntry(Generation, (DomainEvent)fact, null);
        _active.Add(slot, entry);
        if (_selection.Contains(slot))
            _selected.Add(slot, entry);
    }

    /// <summary>Reducer hook for settlement; only real terminal source facts release or fence a key.</summary>
    public void ApplySettled(SourceUsePurpose purpose, ISourceUseFact fact)
    {
        var slot = SourceUseSlot.Of(owner, purpose, fact);
        if (_selected.GetValueOrDefault(slot) is { IsSettled: true })
            throw new InvalidOperationException("A source use is settled at most once.");
        var settled = _active.Remove(slot, out var entry)
            ? entry with { SettleFact = (DomainEvent)fact }
            : new SourceUseEntry(0, null, (DomainEvent)fact);
        _observed = true;
        _verified = false;
        if (_selection.Contains(slot))
            _selected[slot] = settled;
    }

    internal void ObserveOwnerFact(DomainEvent fact)
    {
        _historyDigest = SourceUseHistoryDigest.Append(_historyDigest, fact);
        _ownerFacts = checked(_ownerFacts + 1);
        _verified = false;
    }

    internal void BeginHydration() => _verified = false;

    internal void Confirm(SourceUseHistoryValidation history, ulong ownerVersion, ulong physicalPosition)
    {
        if (_ownerFacts != ownerVersion || _historyDigest != history.Digest ||
            Generation != history.Generation || _active.Count != history.Active.Count ||
            _active.Any(pair => !history.Active.TryGetValue(pair.Key, out var expected) ||
                !SourceUseHistoryValidation.Same(pair.Value, expected)))
            throw new InvalidOperationException("The source-use replay differs from the verified exact native history.");
        if (_selected.Any(pair => !history.Entries.TryGetValue(pair.Key, out var expected) ||
                !SourceUseHistoryValidation.Same(pair.Value, expected)))
            throw new InvalidOperationException("The selected source-use history differs from its actual replayed native metadata.");
        _selected.Clear();
        foreach (var slot in _selection)
            if (history.Entries.TryGetValue(slot, out var entry))
                _selected.Add(slot, entry);
        _verifiedVersion = ownerVersion;
        _verifiedPosition = physicalPosition;
        _verified = true;
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

}
