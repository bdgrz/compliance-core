using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Exact digest-verified immutable source bytes physically sealed in the published close source envelope.</summary>
public sealed record TypeIIPeriodCloseContentBlob(ImmutableContentRef Reference, string Base64);
