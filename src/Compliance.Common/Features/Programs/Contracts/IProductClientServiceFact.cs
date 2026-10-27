using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Programs.Contracts;

public interface IProductClientServiceFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid ServiceId { get; }
    Uuid ProgramId { get; }
    long Revision { get; }
    ImmutableContentRef Content { get; }
    ImmutableContentRef? OperationResult { get; }
    DateTimeOffset ChangedAt { get; }
    ulong NativeSequence { get; }
}
