using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Models;

public sealed record TrainingRequirementRegistration(Uuid RequirementId, string Identifier,
    long Version);
