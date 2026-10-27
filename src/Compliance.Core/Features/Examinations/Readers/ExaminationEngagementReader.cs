using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

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
