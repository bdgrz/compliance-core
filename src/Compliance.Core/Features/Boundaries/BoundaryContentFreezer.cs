using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public static class BoundaryContentFreezer
{
    public static async ValueTask<FrozenBoundaryMutation> FreezeAsync(IImmutableContentStore content, BoundaryRules after,
        BoundaryMutationContent mutation, CancellationToken ct = default)
    {
        if (after.CurrentContent is not { } current)
            throw new ImmutableContentIntegrityException("The prepared boundary has no authored content.");
        var realm = after.TenantId.ToString();
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(after.Id,
            BoundaryFactContent.ContentSchema + ":" + AuthoredIntentDigest.Compute(current)),
            BoundaryFactContent.ContentSchema, current, ct).ConfigureAwait(false);
        var header = after.Header(body);
        var state = after.Authority(header);
        var authority = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(after.Id,
            BoundaryFactContent.AuthoritySchema + ":" + AuthoredIntentDigest.Compute(state)),
            BoundaryFactContent.AuthoritySchema, state, ct).ConfigureAwait(false);
        var change = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), BoundaryFactContent.ChangeSchema, mutation, ct).ConfigureAwait(false);
        return new FrozenBoundaryMutation(header, change, authority);
    }
}
