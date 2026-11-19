using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Commitments.Governance.Models;
namespace Bdgrz.Compliance.Features.Commitments.Governance.Events;
public abstract record CommitmentGovernanceFact(Uuid TenantId, Uuid ProgramId, Uuid CommitmentId,
    long Revision, Uuid CommandId, string IntentSha256, Uuid ActorId, DateTimeOffset OccurredAt) : DomainEvent;
