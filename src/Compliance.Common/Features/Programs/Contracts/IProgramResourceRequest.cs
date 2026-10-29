using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Programs.Contracts;

/// <summary>A request authorized through the program that owns an existing resource.</summary>
public interface IProgramResourceRequest : IProgramManagementRequest
{
    Uuid ResourceId { get; }
    ProgramResourceKind ResourceKind { get; }

    string RequiredPermission => IProgramScopedRequest.ManagementPermission;
}

/// <summary>A read request authorized through the program that owns an existing resource.</summary>
public interface IProgramReadResourceRequest : IProgramResourceRequest
{
    string IProgramResourceRequest.RequiredPermission => IProgramReadRequest.ReadPermission;
}
