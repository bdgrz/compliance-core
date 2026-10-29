using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Readers;
namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class GetTeamHandler(ITeamDirectoryReader directory) : IRequestHandler<GetTeam, TeamView>
{
    public async ValueTask<Result<TeamView>> HandleAsync(IRequestContext<GetTeam> context, CancellationToken ct)
    {
        var team = await directory.GetAsync(context.Request.TenantId, context.Request.TeamId, ct);
        return team is null
            ? Result<TeamView>.Failure(new RequestError(RequestErrorKind.NotFound, "The team was not found."))
            : Result<TeamView>.Success(team);
    }
}
