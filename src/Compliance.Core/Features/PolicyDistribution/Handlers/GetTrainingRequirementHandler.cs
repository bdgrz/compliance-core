using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

public sealed class GetTrainingRequirementHandler(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
    : IRequestHandler<GetTrainingRequirement, TrainingRequirementView>
{
    public async ValueTask<Result<TrainingRequirementView>> HandleAsync(
        IRequestContext<GetTrainingRequirement> context, CancellationToken ct)
    {
        var request = context.Request;
        var requirement = await reader.HydrateAsync(new TrainingRequirement(request.TenantId,
            request.RequirementId), ct).ConfigureAwait(false);
        if (!requirement.IsDefined || requirement.ProgramId != request.ProgramId)
            return Result<TrainingRequirementView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The training requirement was not found."));
        try
        {
            return await TrainingRequirementRules.ViewAsync(content, events, requirement, request.Version, ct)
                    .ConfigureAwait(false) is { } view
                ? Result<TrainingRequirementView>.Success(view)
                : Result<TrainingRequirementView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The training requirement was not found."));
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Result<TrainingRequirementView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The training requirement content cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }
}
