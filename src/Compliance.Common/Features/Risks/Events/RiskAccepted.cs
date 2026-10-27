using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.accepted", 2)]
public sealed record RiskAccepted(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision, long NativeSequence,
    RiskEvaluationAuthorityHeader Header, ImmutableContentRef ChangeContent, ImmutableContentRef CurrentAuthorityContent,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, IRiskEvaluationFact;
