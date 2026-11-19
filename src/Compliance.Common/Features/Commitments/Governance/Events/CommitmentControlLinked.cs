using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Events;
[Discriminator("bdgrz.commitment.governance.commitmentcontrollinked", 1)]
public sealed record CommitmentControlLinked(Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, Uuid ControlId,
    long Revision, Uuid CommandId, string IntentSha256, Uuid ActorId, DateTimeOffset OccurredAt,
    long CommitmentVersion, long ControlVersion, string Rationale) : DomainEvent;
