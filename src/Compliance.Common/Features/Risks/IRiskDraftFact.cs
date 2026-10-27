using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public interface IRiskDraftFact : INativeOperationFact
{
    RiskDraftAuthorityHeader Header { get; }
    ImmutableContentRef Content { get; }
    ImmutableContentRef? OperationResult { get; }
    long NativeSequence { get; }
}
