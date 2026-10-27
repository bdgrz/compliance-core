using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

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
