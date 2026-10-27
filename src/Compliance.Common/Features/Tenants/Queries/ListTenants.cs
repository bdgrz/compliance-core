using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.Tenants.Queries;

/// <summary>
///     Lists projected organization metadata for a platform operator, including provisioning and
///     suspended organizations. Results may lag the tenant event stream.
/// </summary>
[Discriminator("bdgrz.platform.tenant.list", 1)]
public sealed record ListTenants(int? Limit = null, string? Cursor = null)
    : IRequest<Page<TenantView>>, ICallable, IPlatformOperatorRequest;
