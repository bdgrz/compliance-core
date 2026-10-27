using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Handlers;

public sealed class GetApplicationImportHandler(ApplicationImportReadConsistency consistency)
    : IRequestHandler<GetApplicationImport, ApplicationImportView>
{
    public ValueTask<Result<ApplicationImportView>> HandleAsync(
        IRequestContext<GetApplicationImport> context, CancellationToken ct) =>
        consistency.GetFreshAsync(context.Request.TenantId, context.Request.BatchId,
            context.Request.MinimumRevision, ct);
}
