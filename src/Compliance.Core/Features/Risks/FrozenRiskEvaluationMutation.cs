using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Proofs;
namespace Bdgrz.Compliance.Features.Risks;

public sealed record FrozenRiskEvaluationMutation(RiskEvaluationView Before, RiskEvaluationView After,
    RiskEvaluationAuthorityHeader Header, ImmutableContentRef Change, ImmutableContentRef CurrentAuthority,
    object Mutation, string CommandKind);
