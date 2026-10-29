using Cntryl.Portia;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.ControlMappings.Contracts;

/// <summary>A native line transition with complete immutable authored/current content.</summary>
public interface IMappingLineFact : INativeOperationFact
{
    Uuid TenantId { get; }
    Uuid ProgramId { get; }
    long Revision { get; }
    int VersionNumber { get; }
    ImmutableContentRef Content { get; }
    MappingLineHeader Header { get; }
    ImmutableContentRef? OperationResult { get; }
}
