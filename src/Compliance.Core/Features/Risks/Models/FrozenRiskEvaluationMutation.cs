using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Risks.Models;

public sealed record FrozenRiskEvaluationMutation(RiskEvaluationView Before, RiskEvaluationView After,
    RiskEvaluationAuthorityHeader Header, ImmutableContentRef Change, ImmutableContentRef CurrentAuthority,
    object Mutation, string CommandKind);
