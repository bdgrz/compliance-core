using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Snapshots.Contracts;

public interface ISnapshotProgramResourceRequest : IProgramResourceRequest
{
    Cntryl.Portia.Uuid SnapshotId { get; }
    Cntryl.Portia.Uuid IProgramResourceRequest.ResourceId => SnapshotId;
    ProgramResourceKind IProgramResourceRequest.ResourceKind => ProgramResourceKind.Snapshot;
}

public interface ISnapshotProgramReadRequest : ISnapshotProgramResourceRequest,
    IProgramReadResourceRequest;
