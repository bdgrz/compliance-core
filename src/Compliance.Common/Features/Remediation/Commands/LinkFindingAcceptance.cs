using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Commands;

/// <summary>
///     Links an approved risk acceptance (kind risk_acceptance with the risk ID and acceptance ID)
///     or an approved waiver (kind waiver with the waiver ID). It never closes the finding.
/// </summary>
[Discriminator("bdgrz.finding.acceptance.link", 2)]
public sealed record LinkFindingAcceptance(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, string Kind, Uuid RecordId, Uuid? DecisionId = null)
    : IRequest<FindingView>, IProgramScopedRequest, IClientManagementMutationRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
