using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Deterministic slug claims and tenant answers dispatched by internal reactors. The command id of every effect
///     derives from its source fact, so a replayed source fact repeats the same logical operation and the owner
///     replays its original acceptance instead of appending a second one.
/// </summary>
static class TenantSlugEffects
{
    /// <summary>The claim a tenant fact causes on the slug owner.</summary>
    public static RegisterTenantSlug Register(Uuid sourceEventId, Uuid tenantId, string slug) =>
        new(CommandId(sourceEventId, "tenant-slug:register", tenantId, slug), tenantId, slug);

    /// <summary>The surrender a tenant fact causes on the slug owner.</summary>
    public static SurrenderTenantSlug Surrender(Uuid sourceEventId, Uuid tenantId, string slug) =>
        new(CommandId(sourceEventId, "tenant-slug:surrender", tenantId, slug), tenantId, slug);

    /// <summary>The confirmation a registered claim causes on the tenant.</summary>
    public static ConfirmTenantSlug Confirm(Uuid sourceEventId, Uuid tenantId, string slug) =>
        new(CommandId(sourceEventId, "tenant:confirm-slug", tenantId, slug), tenantId, slug);

    /// <summary>The rejection a refused claim causes on the tenant.</summary>
    public static RejectTenantSlug Reject(Uuid sourceEventId, Uuid tenantId, string slug) =>
        new(CommandId(sourceEventId, "tenant:reject-slug", tenantId, slug), tenantId, slug);

    /// <summary>The confirmation a released slug causes on the tenant.</summary>
    public static ConfirmTenantSlugSurrender ConfirmSurrender(Uuid sourceEventId, Uuid tenantId, string slug) =>
        new(CommandId(sourceEventId, "tenant:confirm-slug-surrender", tenantId, slug), tenantId, slug);

    /// <summary>The rejection a refused surrender causes on the tenant.</summary>
    public static RejectTenantSlugSurrender RejectSurrender(Uuid sourceEventId, Uuid tenantId, string slug) =>
        new(CommandId(sourceEventId, "tenant:reject-slug-surrender", tenantId, slug), tenantId, slug);

    static Uuid CommandId(Uuid sourceEventId, string effectKind, Uuid tenantId, string slug) =>
        EffectIdentity.CommandId(sourceEventId, effectKind, $"{tenantId}:{slug}", null);
}
