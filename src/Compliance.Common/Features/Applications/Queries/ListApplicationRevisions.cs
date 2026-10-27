using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application.revision.list", 1)]
public sealed record ListApplicationRevisions(Uuid TenantId, Uuid ApplicationId,
    int? Limit = null, string? Cursor = null, long? MinimumApplicationRevision = null)
    : IRequest<Page<ApplicationRevisionView>>, IRestrictedApplicationResourceReadRequest, ICallable;
