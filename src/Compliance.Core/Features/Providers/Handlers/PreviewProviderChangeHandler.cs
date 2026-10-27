using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Queries;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Providers.Handlers;

public sealed class PreviewProviderChangeHandler(ProviderChangeImpactService impact)
    : IRequestHandler<PreviewProviderChange, ProviderChangeImpactPreview>
{
    public ValueTask<Result<ProviderChangeImpactPreview>> HandleAsync(
        IRequestContext<PreviewProviderChange> context, CancellationToken ct)
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        return impact.PreviewAsync(context.Request, userId, ct);
    }
}
