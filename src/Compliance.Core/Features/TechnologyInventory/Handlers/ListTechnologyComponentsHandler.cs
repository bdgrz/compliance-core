using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Queries;
using Bdgrz.Compliance.Features.TechnologyInventory.Readers;
namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

public sealed class ListTechnologyComponentsHandler(ITechnologyInventoryReader directory,
    TechnologyInventoryReadConsistency consistency) : IRequestHandler<ListTechnologyComponents, Page<TechnologyComponentView>>
{
    public async ValueTask<Result<Page<TechnologyComponentView>>> HandleAsync(
        IRequestContext<ListTechnologyComponents> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TechnologyComponentView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The technology component list limit must be between 1 and 200."));
        var ready = await consistency.EnsureListCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<Page<TechnologyComponentView>>.Failure(ready.Error);
        Page<TechnologyComponentView> page;
        try
        {
            page = await directory.ListComponentsAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TechnologyComponentView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The technology component cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<TechnologyComponentView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The technology component list was not found."))
            : Result<Page<TechnologyComponentView>>.Success(page);
    }
}
