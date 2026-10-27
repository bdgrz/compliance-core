using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Policies.Rules;

public sealed record FrozenPolicyMutation(PolicyAuthorityHeader Header, ImmutableContentRef Change, ImmutableContentRef Authority);
