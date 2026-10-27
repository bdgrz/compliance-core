using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Policies;

public sealed record FrozenPolicyMutation(PolicyAuthorityHeader Header, ImmutableContentRef Change, ImmutableContentRef Authority);
