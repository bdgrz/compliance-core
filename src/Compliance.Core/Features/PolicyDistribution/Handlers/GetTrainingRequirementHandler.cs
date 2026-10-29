using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
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
