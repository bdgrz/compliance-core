using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>The authored intent of an invitation acceptance; the token is a secret and never part of it.</summary>
public sealed record TenantInvitationAcceptanceIntent(Uuid InvitationId, Uuid TenantId, string EmailAddress);
