using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Queries;
namespace Bdgrz.Compliance.Features.Tenants.Handlers;

public sealed class ListPlatformOperatorsHandler(IAggregateReader reader, IImmutableContentStore content)
    : IRequestHandler<ListPlatformOperators, PlatformOperatorRosterView>
{
    public async ValueTask<Result<PlatformOperatorRosterView>> HandleAsync(
        IRequestContext<ListPlatformOperators> context, CancellationToken ct)
    {
        var roster = await reader.HydrateAsync(new PlatformOperatorRoster(), ct).ConfigureAwait(false);
        var authority = await PlatformOperatorRosterContent.ReadAsync(content, roster, ct).ConfigureAwait(false);
        return Result<PlatformOperatorRosterView>.Success(new PlatformOperatorRosterView(authority.Revision, authority.Operators));
    }
}
