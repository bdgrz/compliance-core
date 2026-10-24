using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public interface IControlFact : INativeOperationFact
{
    Uuid TenantId { get; }
    ControlAuthorityHeader Header { get; }
    ImmutableContentRef Change { get; }
    ImmutableContentRef Authority { get; }
    long NativeSequence { get; }
    int ChangeCount { get; }
    ImmutableContentRef? OperationResult { get; }
}
