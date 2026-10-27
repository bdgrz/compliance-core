using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Contracts;

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
