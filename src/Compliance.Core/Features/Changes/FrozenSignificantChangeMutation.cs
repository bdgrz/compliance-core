using Bdgrz.Compliance.Features.Changes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Changes;

public sealed record FrozenSignificantChangeMutation(SignificantChangeAuthorityHeader Header,
    ImmutableContentRef Body, ImmutableContentRef Change, SignificantChangeMutation Mutation,
    SignificantChangeView? Before, SignificantChangeView After);
