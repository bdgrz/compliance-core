using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
namespace Bdgrz.Compliance.Features.Applications.Queries;

/// <summary>
/// Lists the access-review scope status of every system instance of one application from the
/// Fitz scope projection, for readiness visibility.
/// </summary>
[Discriminator("bdgrz.application.access_review_scopes.list", 1)]
public sealed record ListAccessReviewScopes(Uuid TenantId, Uuid ApplicationId,
    DateTimeOffset? AsOf = null, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AccessReviewScopeStatusView>>, IRestrictedApplicationResourceReadRequest,
        ICallable;
