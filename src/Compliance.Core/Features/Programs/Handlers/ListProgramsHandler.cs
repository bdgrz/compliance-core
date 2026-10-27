using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Queries;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.Programs.Handlers;

public sealed class ListProgramsHandler(IProgramDirectoryReader directory,
    IAccessGrantPermissionAuthorizer access)
    : IRequestHandler<ListPrograms, Page<ProgramView>>
{
    public async ValueTask<Result<Page<ProgramView>>> HandleAsync(
        IRequestContext<ListPrograms> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The program list limit must be between 1 and 200."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Program discovery requires a Bdgrz user identity."));
        var visibility = await access.GetProgramVisibilityAsync(request.TenantId, userId,
                RbacIds.Member(request.TenantId, userId), IProgramReadRequest.ReadPermission, ct)
            .ConfigureAwait(false);

        if (visibility.OrganizationWide)
        {
            try
            {
                var page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                    request.Cursor, ct).ConfigureAwait(false);
                return ValidatePage(request.TenantId, page);
            }
            catch (KvDirectoryQueryException)
            {
                return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The program cursor is invalid."));
            }
        }

        var programs = new List<ProgramView>();
        var cursor = request.Cursor;
        do
        {
            var remaining = (request.Limit ?? 50) - programs.Count;
            Page<ProgramView> page;
            try
            {
                page = await directory.ListAsync(request.TenantId, remaining, cursor, ct)
                    .ConfigureAwait(false);
            }
            catch (KvDirectoryQueryException)
            {
                return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.Validation,
                    "The program cursor is invalid."));
            }
            if (page.Items.Any(item => item.TenantId != request.TenantId))
                return Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The programs were not found."));

            programs.AddRange(page.Items.Where(item => visibility.ProgramIds.Contains(item.ProgramId)));
            cursor = page.NextCursor;
        } while (programs.Count < (request.Limit ?? 50) && cursor is not null);

        return Result<Page<ProgramView>>.Success(new Page<ProgramView>(programs, cursor));
    }

    static Result<Page<ProgramView>> ValidatePage(Uuid tenantId, Page<ProgramView> page) =>
        page.Items.Any(item => item.TenantId != tenantId)
            ? Result<Page<ProgramView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The programs were not found."))
            : Result<Page<ProgramView>>.Success(page);
}
