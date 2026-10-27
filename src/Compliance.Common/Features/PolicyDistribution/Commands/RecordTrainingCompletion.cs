using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>
///     Records a reviewed manual or LMS-export completion for one person. The campaign admits the complete result
///     first (<paramref name="ExpectedCampaignRevision" /> is the campaign's logical revision).
/// </summary>
[Discriminator("bdgrz.training_completion.record", 2)]
public sealed record RecordTrainingCompletion(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    Uuid PersonId, long RequirementVersion, DateOnly CompletedOn, string Source, string EvidenceReference,
    long ExpectedCampaignRevision)
    : IRequest<OperationAcceptedResponse>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
