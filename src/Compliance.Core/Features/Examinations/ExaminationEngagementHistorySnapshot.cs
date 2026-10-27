using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Verified native historical lookup outside the compact command owner.</summary>
public sealed class ExaminationEngagementHistorySnapshot
{
    internal ExaminationEngagementHistorySnapshot(ExaminationEngagement owner, IReadOnlyList<DomainEvent> facts,
        IReadOnlyDictionary<Uuid, ImmutableContentRef> baselines)
    {
        Facts = Array.AsReadOnly(facts.ToArray());
        Baselines = baselines;
        Cut = ExaminationEngagementPreparedCut.Capture(owner);
    }
    public IReadOnlyList<DomainEvent> Facts { get; }
    internal IReadOnlyDictionary<Uuid, ImmutableContentRef> Baselines { get; }
    internal ExaminationEngagementPreparedCut Cut { get; }
    public ImmutableContentRef? BaselineContent(Uuid id) => Baselines.GetValueOrDefault(id);
    public DomainEvent? BaselineFact(Uuid id) => Facts.FirstOrDefault(fact =>
        fact is IExaminationEngagementFact source && source.BaselineId == id);
}
