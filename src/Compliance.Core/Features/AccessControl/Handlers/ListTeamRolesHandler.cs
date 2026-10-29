using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
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
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class ListTeamRolesHandler(ITeamRoleDirectoryReader directory)
    : IRequestHandler<ListTeamRoles, Page<TeamRoleView>>
{
    public async ValueTask<Result<Page<TeamRoleView>>> HandleAsync(
        IRequestContext<ListTeamRoles> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TeamRoleView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team role list limit must be between 1 and 200."));
        Page<TeamRoleView> page;
        try
        {
            page = await directory.ListAsync(
                request.TenantId,
                request.TeamId,
                request.Limit ?? 50,
                request.Cursor,
                ListRequestNormalization.NormalizeSearch(request.Search),
                ListRequestNormalization.IsDescending(request.Sort),
                ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TeamRoleView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team role cursor is invalid."));
        }
        return page.Items.Any(item => item.TeamId != request.TeamId)
            ? Result<Page<TeamRoleView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The team roles were not found."))
            : Result<Page<TeamRoleView>>.Success(page);
    }
}
