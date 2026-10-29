using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record FrozenCommitmentMutation(CommitmentAuthorityHeader Header, ImmutableContentRef Change, ImmutableContentRef Authority);
