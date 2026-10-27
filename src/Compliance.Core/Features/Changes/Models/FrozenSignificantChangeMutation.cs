using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Changes.Models;

public sealed record FrozenSignificantChangeMutation(SignificantChangeAuthorityHeader Header,
    ImmutableContentRef Body, ImmutableContentRef Change, SignificantChangeMutation Mutation,
    SignificantChangeView? Before, SignificantChangeView After);
