using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Commitments.Services;

public static class CommitmentContent
{
    public static async ValueTask<FrozenCommitmentMutation> FreezeAsync(IImmutableContentStore content, CommitmentRules after,
        CommitmentMutationContent mutation, CancellationToken ct = default)
    {
        if (after.CurrentContent is not { } current || CommitmentRules.Validate(after.Kind!, after.Identifier!, current.Statement,
            current.Context, current.SourceReference, after.ProviderId) is not null)
            throw new ImmutableContentIntegrityException("The prepared commitment has no valid current authored content.");
        var realm = after.TenantId.ToString();
        var body = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(after.Id,
            CommitmentFactContent.ContentSchema + ":" + AuthoredIntentDigest.Compute(current)),
            CommitmentFactContent.ContentSchema, current, ct).ConfigureAwait(false);
        var header = after.Header(body);
        var state = after.Authority(header);
        var authority = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(after.Id,
            CommitmentFactContent.AuthoritySchema + ":" + AuthoredIntentDigest.Compute(state)),
            CommitmentFactContent.AuthoritySchema, state, ct).ConfigureAwait(false);
        var change = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), CommitmentFactContent.ChangeSchema, mutation, ct).ConfigureAwait(false);
        return new FrozenCommitmentMutation(header, change, authority);
    }
}
