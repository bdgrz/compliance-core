using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Changes;

public sealed record FrozenSignificantChangeMutation(SignificantChangeAuthorityHeader Header,
    ImmutableContentRef Body, ImmutableContentRef Change, SignificantChangeMutation Mutation,
    SignificantChangeView? Before, SignificantChangeView After);
