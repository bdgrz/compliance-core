using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Hydrates the governed application a single owner-local decision sees. A target's own stream replays only its
///     own facts: a provisional import effect never changes the published content, a publication shows when the
///     target's own settlement fact exists, and a canceled effect never advances the public revision. Responses that
///     compose several targets or can expose a batch publication that has not settled read through
///     <see cref="ApplicationPublicationReader" />, which fixes one global cut.
/// </summary>
static class ApplicationImportVisibility
{
    public static async ValueTask<DeclaredApplication> HydrateApplicationAsync(this IAggregateReader reader,
        Uuid tenantId, Uuid applicationId, CancellationToken ct = default) =>
        await reader.HydrateAsync(new DeclaredApplication(tenantId, applicationId), ct).ConfigureAwait(false);
}
