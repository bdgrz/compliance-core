using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Projections;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Projectors;

public sealed partial class TrainingRequirementDirectoryProjector(ITrainingRequirementDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(TrainingRequirement.Area),
            FitzTrainingRequirementDirectory.ProjectorName),
        IProjectorHandler<TrainingRequirementDefined>
{
    public async ValueTask<Result> HandleAsync(IProjectorContext<TrainingRequirementDefined> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
