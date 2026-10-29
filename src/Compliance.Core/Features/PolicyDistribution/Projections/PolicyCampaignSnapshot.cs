using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Projections;

/// <summary>Explicit verified native history and current read composition outside the campaign command owner.</summary>
public sealed class PolicyCampaignSnapshot(PolicyDistributionCampaign owner, IReadOnlyList<DomainEvent> facts)
{
    public PolicyDistributionCampaign Owner { get; } = owner;
    public IReadOnlyList<DomainEvent> Facts { get; } = facts;
    public IReadOnlyList<PolicyCampaignAudienceAmended> Amendments { get; } = [.. facts.OfType<PolicyCampaignAudienceAmended>()];
    public IReadOnlyList<PolicyParticipantResultAdmitted> Admitted { get; } = [.. facts.OfType<PolicyParticipantResultAdmitted>()];
    public Uuid Id => Owner.Id;
    public Uuid TenantId => Owner.TenantId;
    public EventStreamAddress Stream => Owner.Stream;
    public ulong Version => Owner.Version;
    public ulong CommittedStreamPosition => Owner.CommittedStreamPosition;
    public NativeAcceptance Acceptance => Owner.Acceptance;
    public bool IsLaunched => Owner.IsLaunched;
    public bool IsClosed => Owner.IsClosed;
    public bool IsSealed => Owner.IsSealed;
    public Uuid LatestRosterSnapshotId => Owner.LatestRosterSnapshotId;
    public long Revision => Owner.Revision;
    public long Generation => Owner.Generation;
    public long Reconciliations => Owner.Reconciliations;
    public Uuid ProgramId => Owner.ProgramId;
    public Uuid OwnerMemberId => Owner.OwnerMemberId;
    public DateTimeOffset LaunchedAt => Owner.LaunchedAt;
    public CampaignSubject? Subject => Owner.Subject;
    public PolicyCampaignLaunched? Launch => Owner.Launch;
    public PolicyCampaignAudienceFrozen? Frozen => Owner.Frozen;
    public PolicyCampaignClosureAccepted? PendingClosure => Owner.PendingClosure;
    public PolicyCampaignClosed? Closing => Owner.Closing;
    public bool HasReconciled(Uuid snapshotId) => Launch?.RosterSnapshotId == snapshotId ||
        Amendments.Any(value => value.RosterSnapshotId == snapshotId);
    public PolicyParticipantResultAdmitted? AdmittedFor(Uuid rootOperationId) =>
        Admitted.SingleOrDefault(value => value.Operation.RootOperationId == rootOperationId);
    public IReadOnlyList<PolicyParticipantResultAdmitted> AdmittedFor(EventStreamAddress participant) =>
        [.. Admitted.Where(value => value.Operation.AffectedOwner == participant)];
    public PolicyCampaignClosureAccepted? ClosureFor(Uuid rootOperationId) => Facts.OfType<PolicyCampaignClosureAccepted>()
        .SingleOrDefault(value => value.Operation.RootOperationId == rootOperationId);
    public PolicyCampaignClosureCanceled? CanceledFor(Uuid rootOperationId) => Facts.OfType<PolicyCampaignClosureCanceled>()
        .SingleOrDefault(value => value.Operation.RootOperationId == rootOperationId);
}
