using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>All verified immutable references published before a Control owner's native accepting append.</summary>
public sealed record FrozenControlMutation(ControlAuthorityHeader Header, ImmutableContentRef Change,
    ImmutableContentRef Authority);
