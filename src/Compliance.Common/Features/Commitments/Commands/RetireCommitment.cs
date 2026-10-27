using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Commitments.Commands;

/// <summary>Records the retire commitment intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.commitments.retire-commitment", 2)]
public sealed record RetireCommitment(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid CommitmentId, long ExpectedRevision, DateOnly EffectiveOn, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
