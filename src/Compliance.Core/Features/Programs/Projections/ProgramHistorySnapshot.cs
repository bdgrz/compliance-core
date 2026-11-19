using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Programs.Projections;

/// <summary>The verified complete native prefix at one hydrated program cut, composed outside command authority.</summary>
public sealed record ProgramHistorySnapshot(ComplianceProgram Owner, IReadOnlyList<DomainEvent> Facts)
{
    public ProgramCriteriaEditionSelected? CriteriaFactAt(DateTimeOffset asOf)
    {
        ProgramCriteriaEditionSelected? selected = null;
        foreach (var fact in Facts.OfType<ProgramCriteriaEditionSelected>())
        {
            if (fact.ChangeCount != 1 || fact.ChangedAt > asOf ||
                selected is not null && (fact.ChangedAt < selected.ChangedAt ||
                    fact.ChangedAt == selected.ChangedAt && fact.Revision <= selected.Revision))
                continue;
            selected = fact;
        }
        return selected;
    }

    public ProgramCriteriaEditionSelection? CriteriaSelectionAt(DateTimeOffset asOf)
    {
        var fact = CriteriaFactAt(asOf);
        var selection = fact is null ? null :
            new ProgramCriteriaEditionSelection(fact.Revision, fact.EditionId, fact.ChangedAt);
        foreach (var created in Facts.OfType<ProgramCreated>())
        {
            if (created.CriteriaEditionId is not { } edition || created.ChangedAt > asOf ||
                selection is not null && (created.ChangedAt < selection.SelectedAt ||
                    created.ChangedAt == selection.SelectedAt && created.Revision <= selection.Revision))
                continue;
            selection = new ProgramCriteriaEditionSelection(created.Revision, edition, created.ChangedAt);
        }
        return selection;
    }
}
