using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Contracts;

public interface IRiskDraftFact : INativeOperationFact
{
    RiskDraftAuthorityHeader Header { get; }
    ImmutableContentRef Content { get; }
    ImmutableContentRef? OperationResult { get; }
    long NativeSequence { get; }
}
