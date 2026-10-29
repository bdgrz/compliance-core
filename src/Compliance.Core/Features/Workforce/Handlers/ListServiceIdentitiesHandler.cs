using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Queries;
using Bdgrz.Compliance.Features.Workforce.Readers;
namespace Bdgrz.Compliance.Features.Workforce.Handlers;

public sealed class ListServiceIdentitiesHandler(IServiceIdentityDirectoryReader directory,
    ServiceIdentityReadConsistency consistency, ServiceIdentityOwnership ownership)
    : IRequestHandler<ListServiceIdentities, Page<ServiceIdentityView>>
{
    public async ValueTask<Result<Page<ServiceIdentityView>>> HandleAsync(
        IRequestContext<ListServiceIdentities> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ServiceIdentityView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The service identity list limit must be between 1 and 200."));
        var ready = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<ServiceIdentityView>>.Failure(ready.Error);
        Page<ServiceIdentityView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ServiceIdentityView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The service identity cursor is invalid."));
        }
        if (page.Items.Any(item => item.TenantId != request.TenantId))
            return Result<Page<ServiceIdentityView>>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The service identities were not found."));
        var evaluated = await ownership.EvaluateAsync(request.TenantId, page.Items, ct)
            .ConfigureAwait(false);
        if (!evaluated.IsSuccess)
            return Result<Page<ServiceIdentityView>>.Failure(evaluated.Error);
        return Result<Page<ServiceIdentityView>>.Success(new Page<ServiceIdentityView>(
            [.. evaluated.Value.Where(item => request.UnownedOnly != true || item.Unowned)],
            page.NextCursor));
    }
}
