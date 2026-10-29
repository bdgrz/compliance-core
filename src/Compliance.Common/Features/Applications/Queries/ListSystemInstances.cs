using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.system_instance.list", 1)]
public sealed record ListSystemInstances(Uuid TenantId, Uuid ApplicationId,
    int? Limit = null, string? Cursor = null, long? MinimumApplicationRevision = null)
    : IRequest<Page<SystemInstanceView>>, IRestrictedApplicationResourceReadRequest, ICallable;
