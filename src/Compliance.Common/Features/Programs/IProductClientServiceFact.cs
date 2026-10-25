using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public interface IProductClientServiceFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid ServiceId { get; }
    Uuid ProgramId { get; }
    long Revision { get; }
    ImmutableContentRef Content { get; }
    DateTimeOffset ChangedAt { get; }
    ulong NativeSequence { get; }
}
