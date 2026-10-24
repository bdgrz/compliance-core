using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Publishes complete current authority and operation changes without putting read-history snapshots in the owner.</summary>
public static class ControlContent
{
    public static async ValueTask<FrozenControlMutation> FreezeAsync(IImmutableContentStore content,
        ControlRules after, ControlMutationContent mutation, CancellationToken ct = default)
    {
        if (after.CurrentContent is not { } current || ControlRules.ValidateContent(current) is not null)
            throw new ImmutableContentIntegrityException("The prepared Control has no valid authored current content.");
        var realm = after.TenantId.ToString();
        var draft = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(after.Id,
            ControlFactContent.DraftSchema + ":" + AuthoredIntentDigest.Compute(current)),
            ControlFactContent.DraftSchema, current, ct).ConfigureAwait(false);
        var header = after.Header(draft);
        var state = after.Authority(header);
        var authority = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(after.Id,
            ControlFactContent.AuthoritySchema + ":" + AuthoredIntentDigest.Compute(state)),
            ControlFactContent.AuthoritySchema, state, ct).ConfigureAwait(false);
        var change = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
            ControlFactContent.ChangeSchema, mutation, ct).ConfigureAwait(false);
        return new FrozenControlMutation(header, change, authority);
    }
}
