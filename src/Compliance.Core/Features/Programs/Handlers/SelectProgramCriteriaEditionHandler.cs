using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class SelectProgramCriteriaEditionHandler(ProgramOperations operations)
    : IRequestHandler<SelectProgramCriteriaEdition>
{
    public ValueTask<Result> HandleAsync(IRequestContext<SelectProgramCriteriaEdition> context,
        CancellationToken ct) => operations.SelectAsync(context, ct);
}
