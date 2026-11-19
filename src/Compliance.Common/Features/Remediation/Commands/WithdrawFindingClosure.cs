using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>The proposer withdraws the finding's exact current closure proposal.</summary>
[Discriminator("bdgrz.finding.closure.withdraw", 1)]
public sealed record WithdrawFindingClosure(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, Uuid ClosureId, long ProposalRevision)
    : IRequest<FindingView>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
