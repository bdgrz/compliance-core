using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application.list", 1)]
public sealed record ListApplications(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ApplicationView>>, IRestrictedApplicationResourceReadRequest, ICallable;
