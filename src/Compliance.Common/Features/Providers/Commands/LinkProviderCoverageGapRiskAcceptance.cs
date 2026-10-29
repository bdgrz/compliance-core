using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Providers.Commands;

/// <summary>
///     Links an existing R1-07 acceptance without resolving or closing the provider gap.
///     <paramref name="CommandId" /> is the logical identity and becomes the id of the immutable link;
///     <paramref name="ExpectedGapRevision" /> is the revision of the gap owner, which serializes the open-gap and
///     duplicate (risk, acceptance) checks at admission.
/// </summary>
[Discriminator("bdgrz.provider.coverage_gap.link_risk_acceptance", 1)]
public sealed record LinkProviderCoverageGapRiskAcceptance(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    Uuid ProviderId, Uuid GapId, long ExpectedGapRevision, Uuid RiskId, Uuid AcceptanceId)
    : IRequest<OperationAcceptedResponse>, IProviderManagementRequest, IClientManagementMutationRequest,
        IProgramScopedRequest, IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
