using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Contracts;

public interface IRiskEvaluationFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid ProgramId { get; }
    Uuid RiskId { get; }
    long Revision { get; }
    long NativeSequence { get; }
    RiskEvaluationAuthorityHeader Header { get; }
    ImmutableContentRef ChangeContent { get; }
    ImmutableContentRef CurrentAuthorityContent { get; }
    ImmutableContentRef? OperationResult { get; }
}
