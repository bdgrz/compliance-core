using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>
///     Admits owned corrective work on the finding. <paramref name="ActionId" /> is the logical operation identity and
///     the action identity; the complete action intent and the finding generation are frozen on the finding before the
///     202 acknowledgment, and the action owner is materialized independently. A finding that is closing or closed
///     admits no action.
/// </summary>
[Discriminator("bdgrz.finding.corrective_action.add", 2)]
public sealed record AddCorrectiveAction(Uuid ActionId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedFindingRevision, string Description, Uuid OwnerMemberId, DateOnly DueOn)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => ActionId;
}
