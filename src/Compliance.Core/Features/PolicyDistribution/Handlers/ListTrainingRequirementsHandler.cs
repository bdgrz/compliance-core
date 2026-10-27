using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>Lists the program's requirements from the catalog projection once it has reached the requirement owners.</summary>
public sealed class ListTrainingRequirementsHandler(ITrainingRequirementDirectoryReader directory,
    IDomainEventReader events) : IRequestHandler<ListTrainingRequirements, Page<TrainingRequirementView>>
{
    public async ValueTask<Result<Page<TrainingRequirementView>>> HandleAsync(
        IRequestContext<ListTrainingRequirements> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TrainingRequirementView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "Limit must be between 1 and 200."));
        var checkpoint = await directory.LoadCheckpointAsync(request.TenantId, ct).ConfigureAwait(false);
        await using (var pending = events.ReadAsync(EventStreamPattern.ForPattern(request.TenantId.ToString(),
                         TrainingRequirement.Area), checkpoint.Cursor, ct).GetAsyncEnumerator(ct))
        {
            if (await pending.MoveNextAsync().ConfigureAwait(false))
                return Result<Page<TrainingRequirementView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The training requirement catalog has not reached the source.", isTransient: true));
        }

        Page<TrainingRequirementView> page;
        try
        {
            page = await directory.ListProgramAsync(request.TenantId, request.ProgramId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TrainingRequirementView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The training requirement cursor is invalid."));
        }

        return page.Items.Any(item => item.TenantId != request.TenantId || item.ProgramId != request.ProgramId)
            ? Result<Page<TrainingRequirementView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The training requirement catalog has an invalid program scope."))
            : Result<Page<TrainingRequirementView>>.Success(page);
    }
}
