using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

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
