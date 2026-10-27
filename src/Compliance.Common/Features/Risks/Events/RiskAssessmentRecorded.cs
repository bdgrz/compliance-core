using Cntryl.Portia;
using Bdgrz.Compliance.Features.Risks.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Events;

[Discriminator("bdgrz.risk.assessment.recorded", 2)]
public sealed record RiskAssessmentRecorded(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision, long NativeSequence,
    RiskEvaluationAuthorityHeader Header, ImmutableContentRef ChangeContent, ImmutableContentRef CurrentAuthorityContent,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, IRiskEvaluationFact;
