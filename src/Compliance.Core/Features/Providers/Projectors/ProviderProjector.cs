using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Projectors;

/// <summary>
///     Projects applied provider facts, reading the complete content each one references. Accepted and canceled
///     changes are process facts, not provider versions, and project nothing.
/// </summary>
public sealed partial class ProviderProjector(IProviderProjection projection, IImmutableContentStore content)
    : Projector(projection, EventStreamPattern.ForTenant(Provider.Area), FitzProviderDirectory.ProjectorName),
      IProjectorHandler<ProviderRecorded>, IProjectorHandler<ProviderRevised>,
      IProjectorHandler<ProviderChangeAccepted>, IProjectorHandler<ProviderChangeCanceled>
{
    /// <inheritdoc />
    public async ValueTask HandleAsync(ProviderRecorded ev, IProjectorContext context, CancellationToken ct) =>
        await projection.ApplyAsync(ev, await ReadAsync(ev.TenantId, ev.ContentContent, ct).ConfigureAwait(false), ct)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask HandleAsync(ProviderRevised ev, IProjectorContext context, CancellationToken ct) =>
        await projection.ApplyAsync(ev, await ReadAsync(ev.TenantId, ev.ContentContent, ct).ConfigureAwait(false), ct)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public ValueTask HandleAsync(ProviderChangeAccepted ev, IProjectorContext context, CancellationToken ct) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask HandleAsync(ProviderChangeCanceled ev, IProjectorContext context, CancellationToken ct) =>
        ValueTask.CompletedTask;

    async ValueTask<ProviderContent> ReadAsync(Uuid tenantId, Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef reference,
        CancellationToken ct)
    {
        var bytes = await content.ReadVerifiedAsync(tenantId.ToString(), reference, ct).ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Deserialize(bytes.Span, ComplianceCoreJsonContext.Default.ProviderContent)
            ?? throw new ImmutableContentIntegrityException("The provider content is empty.");
    }
}
