using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>The exact finalized target cut a receiver or root prepared before append.</summary>
sealed record IdentityOwnerPreparationCut(ulong Version, ulong PhysicalPosition, string? Digest, string? MetadataDigest)
{
    public static IdentityOwnerPreparationCut Of(Aggregate owner) => new(owner.Version, owner.CommittedStreamPosition,
        owner switch
        {
            UserIdentity identity => identity.ReplayCut.Digest,
            EmailAddress address => address.ReplayCut.Digest,
            _ => null,
        }, owner switch
        {
            UserIdentity identity => identity.ReplayCut.MetadataDigest,
            EmailAddress address => address.ReplayCut.MetadataDigest,
            _ => null,
        });

    public Result Require(Aggregate owner) => this == Of(owner)
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict,
            "The identity target changed after preparation; retry against its current finalized cut.", true));
}
