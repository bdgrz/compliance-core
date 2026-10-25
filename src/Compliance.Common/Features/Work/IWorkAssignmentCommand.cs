using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>One explicitly identified authored accountability operation on the owning per-work-item stream.</summary>
public interface IWorkAssignmentCommand : IControlOperationRequest, IIdentifiedRequest
{
    Uuid CommandId { get; }
    Uuid WorkItemId { get; }
    long ExpectedRevision { get; }
}
