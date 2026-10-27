using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.Tenants.Queries;

[Discriminator("bdgrz.tenant-invitation.list", 1)]
public sealed record ListTenantInvitations(Uuid TenantId, int? Limit = null,
    string? Cursor = null, string? EmailAddress = null)
    : IRequest<Page<TenantInvitationView>>, IRbacManagementRequest, ICallable;
