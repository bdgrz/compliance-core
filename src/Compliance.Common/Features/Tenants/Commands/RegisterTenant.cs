using System.Text.Json.Serialization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>
///     Creates an organization under the caller's stable tenant identity. Retries retain TenantId and authored
///     creation terms. The authenticated creator becomes the first administrator; FirstAdministratorEmail is
///     retained only so older callers receive an explicit validation error rather than silently selecting someone else.
/// </summary>
[Discriminator("bdgrz.tenant.register", 1)]
[method: JsonConstructor]
public sealed record RegisterTenant([property: JsonRequired] Uuid TenantId, string Name, string Slug, string? LegalName = null,
    string? FirstAdministratorEmail = null) : IRequest<TenantRegistration>, ICallable, IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => TenantId;
}
