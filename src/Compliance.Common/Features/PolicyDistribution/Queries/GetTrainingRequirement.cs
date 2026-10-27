using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Queries;

/// <summary>Reads the latest version, or the exact <c>Version</c> when given.</summary>
[Discriminator("bdgrz.training_requirement.get", 1)]
public sealed record GetTrainingRequirement(Uuid TenantId, Uuid ProgramId,
    Uuid RequirementId, long? Version = null)
    : IRequest<TrainingRequirementView>, IProgramReadRequest, ICallable;
