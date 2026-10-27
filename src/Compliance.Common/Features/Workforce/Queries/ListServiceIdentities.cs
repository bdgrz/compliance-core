using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Workforce.Models;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Queries;

/// <summary>
///     Lists non-human identities ordered by display name. <c>UnownedOnly</c> keeps only unowned
///     work, so a filtered page may hold fewer items than the limit while a cursor remains.
/// </summary>
[Discriminator("bdgrz.workforce.service-identity.list", 1)]
public sealed record ListServiceIdentities(Uuid TenantId, bool? UnownedOnly = null,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<ServiceIdentityView>>, IWorkforceRequest, ICallable;
