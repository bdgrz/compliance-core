using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Commands;

/// <summary>
///     Defines an immutable successor version; launched campaigns keep their version.
///     <paramref name="ExpectedVersion" /> is the sole logical concurrency counter.
/// </summary>
[Discriminator("bdgrz.training_requirement.revise", 2)]
public sealed record ReviseTrainingRequirement(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    Uuid RequirementId, long ExpectedVersion, TrainingRequirementContent Content)
    : IRequest<TrainingRequirementRegistration>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
