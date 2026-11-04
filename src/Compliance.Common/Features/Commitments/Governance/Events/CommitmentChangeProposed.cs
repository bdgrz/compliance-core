using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Events;
[Discriminator("bdgrz.commitment.governance.commitmentchangeproposed", 1)]
public sealed record CommitmentChangeProposed(Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, long Revision, Uuid CommandId, string IntentSha256, Uuid ActorId, DateTimeOffset OccurredAt, CommitmentTerms Terms)
    : CommitmentGovernanceFact(TenantId, ProgramId, CommitmentId, Revision, CommandId, IntentSha256, ActorId, OccurredAt);
