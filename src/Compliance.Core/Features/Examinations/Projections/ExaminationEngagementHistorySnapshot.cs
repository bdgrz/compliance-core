using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Examinations.Contracts;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Examinations.Projections;

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
