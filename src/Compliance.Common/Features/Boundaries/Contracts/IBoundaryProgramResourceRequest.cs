using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Boundaries.Contracts;

public interface IBoundaryProgramResourceRequest : IProgramResourceRequest
{
    Cntryl.Portia.Uuid BoundaryId { get; }
    Cntryl.Portia.Uuid IProgramResourceRequest.ResourceId => BoundaryId;
    ProgramResourceKind IProgramResourceRequest.ResourceKind => ProgramResourceKind.Boundary;
}

public interface IBoundaryProgramReadRequest : IBoundaryProgramResourceRequest, IProgramReadResourceRequest;
