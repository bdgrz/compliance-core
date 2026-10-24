using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>One stable activation identity for one exact committed bootstrap source and target administrator.</summary>
static class TenantActivationEffects
{
    public static ActivateTenant Activate(Uuid sourceEventId, Uuid tenantId, Uuid userId, string? email = null) =>
        new(EffectIdentity.CommandId(sourceEventId, "tenant:activate", $"{tenantId}:{userId}", null), tenantId, userId, email);
}
