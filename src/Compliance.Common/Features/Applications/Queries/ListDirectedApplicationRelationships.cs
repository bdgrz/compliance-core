using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Applications.Models;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Queries;

[Discriminator("bdgrz.application.relationships.list", 1)]
public sealed record ListDirectedApplicationRelationships(Uuid TenantId,
    Uuid ApplicationId, string Direction, int Limit = 50, string? Cursor = null)
    : IRequest<Page<ApplicationRelationshipView>>, IApplicationInventoryRequest, ICallable;
