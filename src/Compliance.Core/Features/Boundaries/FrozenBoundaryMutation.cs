using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record FrozenBoundaryMutation(BoundaryAuthorityHeader Header, ImmutableContentRef Change, ImmutableContentRef Authority);
