using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Queries;
using Bdgrz.Compliance.Features.Providers.Readers;
namespace Bdgrz.Compliance.Features.Providers.Handlers;

public sealed class ListProviderRevisionsHandler(IProviderReader directory, ProviderReadConsistency consistency)
    : IRequestHandler<ListProviderRevisions, Page<ProviderView>>
{
    public async ValueTask<Result<Page<ProviderView>>> HandleAsync(IRequestContext<ListProviderRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ProviderView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The provider revision list limit must be between 1 and 200."));
        var current = await consistency.GetAsync(request.TenantId, request.ProviderId, null, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return Result<Page<ProviderView>>.Failure(current.Error);
        Page<ProviderView> page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId, request.ProviderId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ProviderView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The provider revision cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId || item.ProviderId != request.ProviderId ||
            item.Revision < 1 || item.Revision > current.Value.Revision)
            ? Result<Page<ProviderView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The provider revision projection is incomplete."))
            : Result<Page<ProviderView>>.Success(page);
    }
}
