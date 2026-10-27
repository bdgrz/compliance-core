using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Contracts;

public interface IPersonNativeFact : INativeOperationFact
{
    PersonHeader Header { get; }
    ImmutableContentRef Content { get; }
    ImmutableContentRef Change { get; }
    ImmutableContentRef OperationResult { get; }
    bool Changed { get; }
}
