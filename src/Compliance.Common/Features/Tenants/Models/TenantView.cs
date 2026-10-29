using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>
///     A tenant directory entry. <paramref name="Revision" /> is the tenant revision a slug change must expect; only the
///     single-tenant read fills it.
/// </summary>
public sealed record TenantView(Uuid TenantId, string Name, string Slug, string Status = "active",
    string? LegalName = null, Uuid? OperatorUserId = null, bool RequiresInvitation = false,
    bool RequiresActivation = false, ulong Revision = 0);
