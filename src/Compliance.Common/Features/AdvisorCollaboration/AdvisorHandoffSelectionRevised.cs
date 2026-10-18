using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Records the AdvisorHandoffSelectionRevised domain fact for an advisor handoff.</summary>
[Discriminator("bdgrz.advisor-handoff.selection-revised", 1)]
public sealed record AdvisorHandoffSelectionRevised(Uuid TenantId, Uuid ProgramId, Uuid HandoffId, long Revision, IReadOnlyList<AdvisorRecordVersion> RecordVersions, string ChangeReason, ActorReference RevisedBy, DateTimeOffset RevisedAt) : DomainEvent;
