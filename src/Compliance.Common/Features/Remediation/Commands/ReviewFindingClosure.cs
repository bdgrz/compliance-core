using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>Personally reviews the finding's exact current proposal; approval awaits final source proofs.</summary>
[Discriminator("bdgrz.finding.closure.review", 1)]
public sealed record ReviewFindingClosure(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, Uuid ClosureId, long ProposalRevision, bool Approve, string? Comments = null,
    Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<OperationAcceptedResponse>, IControlOperationRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
