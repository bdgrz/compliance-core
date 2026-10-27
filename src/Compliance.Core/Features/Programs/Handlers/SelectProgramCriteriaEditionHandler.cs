using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Programs.Commands;
namespace Bdgrz.Compliance.Features.Programs.Handlers;

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
