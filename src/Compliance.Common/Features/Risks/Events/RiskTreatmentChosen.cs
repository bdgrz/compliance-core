using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks.Events;

[Discriminator("bdgrz.risk.treatment.chosen", 2)]
public sealed record RiskTreatmentChosen(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision, long NativeSequence,
    RiskEvaluationAuthorityHeader Header, ImmutableContentRef ChangeContent, ImmutableContentRef CurrentAuthorityContent,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, IRiskEvaluationFact;
