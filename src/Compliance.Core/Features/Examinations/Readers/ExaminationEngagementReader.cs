using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.History;
using Bdgrz.Compliance.Features.Examinations.Projections;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Readers;

/// <summary>Composes exact native and immutable engagement/baseline history without retaining it on an owner.</summary>
public sealed class ExaminationEngagementReader(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content)
{
    public ValueTask<ExaminationEngagementReadSnapshot> ReadAsync(Uuid tenant, Uuid program, Uuid examination,
        CancellationToken ct = default) => ReadAsync(tenant, program, examination, default, null, default, ct);

    internal async ValueTask<ExaminationEngagementReadSnapshot> ReadAsync(Uuid tenant, Uuid program, Uuid examination,
        Uuid selectedLogicalId, string? selectedCommandKind, Uuid selectedBaselineId, CancellationToken ct)
    {
        var owner = await reader.HydrateAsync(new ExaminationEngagement(tenant, program, examination,
            selectedLogicalId, selectedCommandKind, selectedBaselineId), ct).ConfigureAwait(false);
        var history = await ExaminationEngagementHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        var rules = new ExaminationEngagementRules(tenant, program, examination);
        if (owner.Header is null || owner.Header.ProgramId != program)
            return new ExaminationEngagementReadSnapshot(owner, null, null, history, rules);
        foreach (var fact in history.Facts)
        {
            var state = await ExaminationEngagementContentReader.ReadAsync(content, fact, ct).ConfigureAwait(false);
            try
            {
                rules.Replay(ExaminationEngagementContentReader.RuleChange(fact, state));
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                throw new ImmutableContentIntegrityException("The immutable engagement history violates its complete business rules: " + exception.Message);
            }
        }
        return new ExaminationEngagementReadSnapshot(owner, rules.Current, rules.Baseline, history, rules);
    }

    public async ValueTask<TypeIBaselineView?> ReadBaselineAsync(Uuid tenant, Uuid program, Uuid examination,
        Uuid baseline, CancellationToken ct = default)
    {
        var snapshot = await ReadAsync(tenant, program, examination, default, null, baseline, ct).ConfigureAwait(false);
        return snapshot.View is null ? null : snapshot.Rules.BaselineHistory.SingleOrDefault(item => item.TypeIBaselineId == baseline);
    }
}
