using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
namespace Bdgrz.Compliance.Features.Applications.Handlers;

public sealed class GetApplicationImportHandler(ApplicationImportReadConsistency consistency)
    : IRequestHandler<GetApplicationImport, ApplicationImportView>
{
    public ValueTask<Result<ApplicationImportView>> HandleAsync(
        IRequestContext<GetApplicationImport> context, CancellationToken ct) =>
        consistency.GetFreshAsync(context.Request.TenantId, context.Request.BatchId,
            context.Request.MinimumRevision, ct);
}
