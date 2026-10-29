using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Events;
namespace Bdgrz.Compliance.Features.Programs.Projections;

/// <summary>The verified complete native prefix at one hydrated program cut, composed outside command authority.</summary>
public sealed record ProgramHistorySnapshot(ComplianceProgram Owner, IReadOnlyList<DomainEvent> Facts)
{
    public ProgramCriteriaEditionSelected? CriteriaFactAt(DateTimeOffset asOf) => Facts.OfType<ProgramCriteriaEditionSelected>()
        .Where(fact => fact.ChangeCount == 1 && fact.ChangedAt <= asOf)
        .OrderByDescending(static fact => fact.ChangedAt).ThenByDescending(static fact => fact.Revision).FirstOrDefault();

    public ProgramCriteriaEditionSelection? CriteriaSelectionAt(DateTimeOffset asOf) => CriteriaFactAt(asOf) is { } fact
        ? new ProgramCriteriaEditionSelection(fact.Revision, fact.EditionId, fact.ChangedAt) : null;
}
