using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryFact : INativeOperationFact
{
    Uuid TenantId { get; }
    BoundaryAuthorityHeader Header { get; }
    ImmutableContentRef Change { get; }
    ImmutableContentRef Authority { get; }
    ImmutableContentRef? OperationResult { get; }
    long NativeSequence { get; }
    int ChangeCount { get; }
}
