using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>Proposes one exact closure revision with resolution evidence and verification rationale.</summary>
[Discriminator("bdgrz.finding.closure.propose", 1)]
public sealed record ProposeFindingClosure(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, Uuid ClosureId, long PreviousProposalRevision, FindingClosureTerms Terms)
    : IRequest<FindingView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
