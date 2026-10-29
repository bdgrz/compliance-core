using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Contracts;

/// <summary>One explicitly identified authored accountability operation on the owning per-work-item stream.</summary>
public interface IWorkAssignmentCommand : IControlOperationRequest, IIdentifiedRequest
{
    Uuid CommandId { get; }
    Uuid WorkItemId { get; }
    long ExpectedRevision { get; }
}
