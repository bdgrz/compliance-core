using Bdgrz.Compliance.Features.AccessControl.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Verified complete designation receipts composed outside compact current authority.</summary>
public sealed class ProfessionalDutyHistorySnapshot(IReadOnlyList<DomainEvent> facts,
    IReadOnlyDictionary<Uuid, ProfessionalDutyChangeAccepted> accepted, IReadOnlyDictionary<Uuid, DomainEvent> terminal)
{
    public IReadOnlyList<DomainEvent> Facts { get; } = facts;
    public ProfessionalDutyChangeAccepted? AcceptedFor(Uuid root) => accepted.GetValueOrDefault(root);
    public DomainEvent? TerminalFor(Uuid root) => terminal.GetValueOrDefault(root);
    public bool Matches(DomainEvent candidate) => candidate.Metadata is not null && Facts.Any(actual =>
        actual.GetType() == candidate.GetType() && actual.Equals(candidate) && actual.Metadata == candidate.Metadata);
}
