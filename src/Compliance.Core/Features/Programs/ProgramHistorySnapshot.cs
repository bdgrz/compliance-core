using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

/// <summary>The verified complete native prefix at one hydrated program cut, composed outside command authority.</summary>
public sealed record ProgramHistorySnapshot(ComplianceProgram Owner, IReadOnlyList<DomainEvent> Facts)
{
    public ProgramCriteriaEditionSelected? CriteriaFactAt(DateTimeOffset asOf) => Facts.OfType<ProgramCriteriaEditionSelected>()
        .Where(fact => fact.ChangeCount == 1 && fact.ChangedAt <= asOf)
        .OrderByDescending(static fact => fact.ChangedAt).ThenByDescending(static fact => fact.Revision).FirstOrDefault();

    public ProgramCriteriaEditionSelection? CriteriaSelectionAt(DateTimeOffset asOf) => CriteriaFactAt(asOf) is { } fact
        ? new ProgramCriteriaEditionSelection(fact.Revision, fact.EditionId, fact.ChangedAt) : null;
}
