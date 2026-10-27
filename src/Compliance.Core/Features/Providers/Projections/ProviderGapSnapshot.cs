using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Events;
namespace Bdgrz.Compliance.Features.Providers.Projections;

/// <summary>Explicit complete verified gap history outside its bounded command owner.</summary>
public sealed class ProviderGapSnapshot(ProviderAssuranceGap owner, IReadOnlyList<DomainEvent> facts)
{
    public ProviderAssuranceGap Owner { get; } = owner;
    public IReadOnlyList<DomainEvent> Facts { get; } = facts;
    public IReadOnlyList<ProviderCoverageGapRiskAcceptanceAdmitted> Admissions { get; } =
        [.. facts.OfType<ProviderCoverageGapRiskAcceptanceAdmitted>()];
    public ProviderCoverageGapRiskAcceptanceAdmitted? AdmissionOf(Uuid linkId) =>
        Admissions.SingleOrDefault(value => value.Operation.LogicalId == linkId);
    public EventStreamAddress Stream => Owner.Stream;
    public Uuid ProviderId => Owner.ProviderId;
    public Uuid GapId => Owner.GapId;
    public bool Exists => Owner.Exists;
    public bool IsOpen => Owner.IsOpen;
    public long Revision => Owner.Revision;
    public ProviderCoverageGapRecorded? Recorded => Owner.Recorded;
    public ProviderCoverageGapClosed? Closed => Owner.Closed;
}
