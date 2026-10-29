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

public sealed class ListRoleTeamsHandler(IRoleTeamDirectoryReader directory)
    : IRequestHandler<ListRoleTeams, Page<RoleTeamView>>
{
    public async ValueTask<Result<Page<RoleTeamView>>> HandleAsync(
        IRequestContext<ListRoleTeams> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<RoleTeamView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The role team list limit must be between 1 and 200."));
        Page<RoleTeamView> page;
        try
        {
            page = await directory.ListAsync(
                request.TenantId,
                request.RoleId,
                request.Limit ?? 50,
                request.Cursor,
                ListRequestNormalization.NormalizeSearch(request.Search),
                ListRequestNormalization.IsDescending(request.Sort),
                ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<RoleTeamView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The role team cursor is invalid."));
        }
        return page.Items.Any(item => item.RoleId != request.RoleId)
            ? Result<Page<RoleTeamView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The role teams were not found."))
            : Result<Page<RoleTeamView>>.Success(page);
    }
}
