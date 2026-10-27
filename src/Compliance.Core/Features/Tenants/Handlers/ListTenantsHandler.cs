using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class ListTenantsHandler(ITenantDirectoryReader directory)
    : IRequestHandler<ListTenants, Page<TenantView>>
{
    public async ValueTask<Result<Page<TenantView>>> HandleAsync(
        IRequestContext<ListTenants> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TenantView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The platform tenant list limit must be between 1 and 200."));
        try
        {
            return Result<Page<TenantView>>.Success(await directory.ListAsync(request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false));
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TenantView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The platform tenant cursor is invalid."));
        }
    }
}
