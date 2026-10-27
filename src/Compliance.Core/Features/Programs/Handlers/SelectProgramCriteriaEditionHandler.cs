using Bdgrz.Compliance.Features.Criteria;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class SelectProgramCriteriaEditionHandler(ProgramOperations operations, ICriteriaCatalog catalog)
    : IRequestHandler<SelectProgramCriteriaEdition>
{
    public ValueTask<Result> HandleAsync(IRequestContext<SelectProgramCriteriaEdition> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var edition = catalog.GetEdition(request.EditionId);
        if (edition is null)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The criteria edition is not available.")));
        if (!edition.IsComplete)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The criteria edition is incomplete and cannot be selected.")));
        return operations.SelectAsync(context, ct);
    }
}
