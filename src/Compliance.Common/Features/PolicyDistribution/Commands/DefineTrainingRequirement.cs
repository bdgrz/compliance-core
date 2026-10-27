using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Defines a requirement's first version. <paramref name="RequirementId" /> is the logical operation identity and
///     must equal the deterministic identity of the tenant, program and normalized identifier.
/// </summary>
[Discriminator("bdgrz.training_requirement.define", 2)]
public sealed record DefineTrainingRequirement(Uuid RequirementId, Uuid TenantId, Uuid ProgramId,
    string Identifier, TrainingRequirementContent Content)
    : IRequest<TrainingRequirementRegistration>, IProgramScopedRequest, IClientManagementMutationRequest,
        IIdentifiedRequest, ICallable
{
    Uuid IIdentifiedRequest.RequestId => RequirementId;
}
