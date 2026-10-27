using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.Workforce;

public interface IPersonNativeFact : INativeOperationFact
{
    PersonHeader Header { get; }
    ImmutableContentRef Content { get; }
    ImmutableContentRef Change { get; }
    ImmutableContentRef OperationResult { get; }
    bool Changed { get; }
}
