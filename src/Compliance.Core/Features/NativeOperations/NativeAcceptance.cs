using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.NativeOperations;

/// <summary>
/// Owner-side acceptance of one selected logical operation. Reducers hand every native fact to
/// <see cref="Observe"/>; at most one accepted fact may carry the selected logical id, a second one is stream
/// corruption. Decisions compare the full <see cref="AcceptedOperationBinding"/>, never a hash alone.
/// </summary>
public sealed class NativeAcceptance(Uuid selectedLogicalId)
{
    DomainEvent? _selected;

    /// <summary>The logical id this hydration was selected for; <see cref="Uuid.Empty"/> selects nothing.</summary>
    public Uuid SelectedLogicalId { get; } = selectedLogicalId;

    /// <summary>The accepted fact carrying the selected logical id, once observed.</summary>
    public DomainEvent? SelectedFact => _selected;

    /// <summary>The accepted operation carried by <see cref="SelectedFact"/>.</summary>
    public INativeOperationFact? SelectedOperation => _selected as INativeOperationFact;

    /// <summary>Records a native fact when it carries the selected logical id.</summary>
    public void Observe(DomainEvent fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (fact is not INativeOperationFact native)
            throw new ArgumentException("Only native operation facts take part in acceptance.", nameof(fact));
        if (SelectedLogicalId == Uuid.Empty || native.Operation.LogicalId != SelectedLogicalId)
            return;
        if (_selected is not null)
            throw new InvalidOperationException(
                $"Logical operation {SelectedLogicalId} is accepted more than once on one owner stream.");
        _selected = fact;
    }

    /// <summary>Compares a candidate binding with the accepted fact for the selected logical id.</summary>
    public NativeAcceptanceDecision Decide(AcceptedOperationBinding candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.LogicalId != SelectedLogicalId)
            throw new ArgumentException("The candidate binding must name the selected logical id.", nameof(candidate));
        return SelectedOperation is not { } accepted ? NativeAcceptanceDecision.None
            : AcceptedOperationBinding.Of(accepted.Operation) == candidate ? NativeAcceptanceDecision.Replayed
            : NativeAcceptanceDecision.Conflict;
    }
}
