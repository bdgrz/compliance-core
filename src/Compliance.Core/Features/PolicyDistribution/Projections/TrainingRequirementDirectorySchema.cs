using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Projections;

static class TrainingRequirementDirectorySchema
{
    public static readonly KvDirectoryIndex<TrainingRequirementView> ByProgramIdentifier = new(
        "by_program_identifier", 1, static requirement =>
        [
            requirement.ProgramId.ToString(),
            requirement.Identifier,
        ]);

    public static readonly KvDirectory<TrainingRequirementView, Uuid> Requirements = new(
        "training_requirements", ComplianceCoreJsonContext.Default.TrainingRequirementView,
        static requirement => requirement.RequirementId, static requirementId => [requirementId.ToString()],
        [ByProgramIdentifier]);
}
