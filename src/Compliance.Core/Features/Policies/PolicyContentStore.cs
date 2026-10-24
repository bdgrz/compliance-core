using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public static class PolicyContentStore
{
    public static async ValueTask<FrozenPolicyMutation> FreezeAsync(IImmutableContentStore content, PolicyRules after,
        PolicyMutationContent mutation, CancellationToken ct = default)
    {
        if (after.CurrentContent is not { } current || PolicyRules.ValidateContent(current) is not null)
            throw new ImmutableContentIntegrityException("The prepared Policy has no valid current content.");
        var realm = after.TenantId.ToString();
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(after.Id,
            PolicyFactContent.ContentSchema + ":" + AuthoredIntentDigest.Compute(current)),
            PolicyFactContent.ContentSchema, current, ct).ConfigureAwait(false);
        var header = after.Header(body);
        var state = after.Authority(header);
        var authority = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(after.Id,
            PolicyFactContent.AuthoritySchema + ":" + AuthoredIntentDigest.Compute(state)),
            PolicyFactContent.AuthoritySchema, state, ct).ConfigureAwait(false);
        var change = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), PolicyFactContent.ChangeSchema,
            mutation, ct).ConfigureAwait(false);
        return new FrozenPolicyMutation(header, change, authority);
    }
}
