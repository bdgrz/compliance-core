using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed record FrozenCommitmentMutation(CommitmentAuthorityHeader Header, ImmutableContentRef Change, ImmutableContentRef Authority);
