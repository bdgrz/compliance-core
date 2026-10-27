using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.system_instance.boundary_references.list", 1)]
/// <summary>Lists current draft and current or historical approved references, after source catchup.</summary>
public sealed record ListSystemInstanceBoundaryReferences(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ApplicationBoundaryReferenceView>>, IRestrictedApplicationResourceReadRequest,
        ICallable;
