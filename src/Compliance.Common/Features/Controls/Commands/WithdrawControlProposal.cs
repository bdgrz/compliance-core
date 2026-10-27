using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Commands;

/// <summary>
///     Withdraws the pending successor draft or retirement proposal at its exact revision. The
///     current approved version and every recorded decision are retained.
/// </summary>
[Discriminator("bdgrz.control.proposal.withdraw", 2)]
public sealed record WithdrawControlProposal(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
