using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Models;

public sealed record FrozenBoundaryMutation(BoundaryAuthorityHeader Header, ImmutableContentRef Change, ImmutableContentRef Authority);
