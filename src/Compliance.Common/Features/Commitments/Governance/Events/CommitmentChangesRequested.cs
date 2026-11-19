using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Events;
[Discriminator("bdgrz.commitment.governance.commitmentchangesrequested", 1)]
public sealed record CommitmentChangesRequested(Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, long Revision, Uuid CommandId, string IntentSha256, Uuid ActorId, DateTimeOffset OccurredAt, string Rationale)
    : CommitmentGovernanceFact(TenantId, ProgramId, CommitmentId, Revision, CommandId, IntentSha256, ActorId, OccurredAt);
