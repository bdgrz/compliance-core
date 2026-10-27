using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed record FrozenCommitmentMutation(CommitmentAuthorityHeader Header, ImmutableContentRef Change, ImmutableContentRef Authority);
