using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Complete verified applied result history; the command owner retains no lifetime result collection.</summary>
public sealed class PolicyCampaignParticipantSnapshot(PolicyCampaignParticipant owner, IReadOnlyList<DomainEvent> facts)
{
    public PolicyCampaignParticipant Owner { get; } = owner;
    public IReadOnlyList<DomainEvent> Facts { get; } = facts;
    public IReadOnlyList<DomainEvent> Results { get; } = [.. facts.Where(value => value is
        PolicyAcknowledgementRecorded or TrainingCompletionRecorded or CampaignWaiverApproved)];
    public PolicyAcknowledgementRecorded? Acknowledgement => Results.OfType<PolicyAcknowledgementRecorded>().SingleOrDefault();
    public TrainingCompletionRecorded? Completion => Results.OfType<TrainingCompletionRecorded>().SingleOrDefault();
    public IReadOnlyList<CampaignWaiverApproved> Waivers { get; } = [.. facts.OfType<CampaignWaiverApproved>()];
    public Uuid Id => Owner.Id;
    public Uuid TenantId => Owner.TenantId;
    public Uuid CampaignId => Owner.CampaignId;
    public Uuid PersonId => Owner.PersonId;
    public EventStreamAddress Stream => Owner.Stream;
    public ulong Version => Owner.Version;
    public ulong CommittedStreamPosition => Owner.CommittedStreamPosition;
    public long Revision => Owner.Revision;
    public NativeAcceptance Acceptance => Owner.Acceptance;
    public SourceUseReservations Uses => Owner.Uses;
    public DomainEvent? ResultFor(Uuid rootOperationId) => Results.SingleOrDefault(value =>
        ((INativeOperationFact)value).Operation.RootOperationId == rootOperationId);
}
