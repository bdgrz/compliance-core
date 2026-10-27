using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Projections;

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
