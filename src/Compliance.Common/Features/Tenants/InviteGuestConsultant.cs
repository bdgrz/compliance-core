using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant.guest-consultant.invite", 1)]
public sealed record InviteGuestConsultant(Uuid TenantId, string EmailAddress,
    IReadOnlyList<Uuid> ProgramIds, DateOnly ExpiresOn)
    : IRequest, IClientRbacMutationRequest, ICallable;
