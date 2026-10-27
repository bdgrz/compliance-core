using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed partial class TrainingRequirementDirectoryProjector(ITrainingRequirementDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(TrainingRequirement.Area),
            FitzTrainingRequirementDirectory.ProjectorName),
        IProjectorHandler<TrainingRequirementDefined>
{
    public ValueTask HandleAsync(TrainingRequirementDefined ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
