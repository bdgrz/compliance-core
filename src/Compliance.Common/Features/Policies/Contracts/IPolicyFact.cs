using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Policies.Contracts;

public interface IPolicyFact : INativeOperationFact
{
    Uuid TenantId { get; }
    PolicyAuthorityHeader Header { get; }
    ImmutableContentRef Change { get; }
    ImmutableContentRef Authority { get; }
    ImmutableContentRef? OperationResult { get; }
    long NativeSequence { get; }
}
