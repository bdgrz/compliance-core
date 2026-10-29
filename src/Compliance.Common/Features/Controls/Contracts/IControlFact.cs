using Cntryl.Portia;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Controls.Contracts;

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
