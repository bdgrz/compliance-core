using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Projections;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Projectors;

/// <summary>
///     Projects applied location and operational process facts, reading the complete content each one references.
///     Accepted and canceled changes are process facts, not versions, and project nothing.
/// </summary>
public sealed partial class InventoryRegisterProjector(IInventoryRegisterProjection projection,
    IImmutableContentStore content)
    : Projector(projection, EventStreamPattern.ForTenant(InventoryRegisters.Area),
            FitzInventoryRegisterDirectory.ProjectorName),
        IProjectorHandler<LocationRevisionRecorded>,
        IProjectorHandler<OperationalProcessRevisionRecorded>,
        IProjectorHandler<LocationChangeAccepted>,
        IProjectorHandler<LocationChangeCanceled>,
        IProjectorHandler<OperationalProcessChangeAccepted>,
        IProjectorHandler<OperationalProcessChangeCanceled>
{
    /// <inheritdoc />
    public async ValueTask HandleAsync(LocationRevisionRecorded ev, IProjectorContext context,
        CancellationToken ct) => await projection.ApplyAsync(ev, await ReadAsync(ev.TenantId, ev.ContentContent,
            ComplianceCoreJsonContext.Default.LocationContent, ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask HandleAsync(OperationalProcessRevisionRecorded ev, IProjectorContext context,
        CancellationToken ct) => await projection.ApplyAsync(ev, await ReadAsync(ev.TenantId, ev.ContentContent,
            ComplianceCoreJsonContext.Default.OperationalProcessContent, ct).ConfigureAwait(false), ct)
        .ConfigureAwait(false);

    /// <inheritdoc />
    public ValueTask HandleAsync(LocationChangeAccepted ev, IProjectorContext context, CancellationToken ct) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask HandleAsync(LocationChangeCanceled ev, IProjectorContext context, CancellationToken ct) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask HandleAsync(OperationalProcessChangeAccepted ev, IProjectorContext context,
        CancellationToken ct) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask HandleAsync(OperationalProcessChangeCanceled ev, IProjectorContext context,
        CancellationToken ct) => ValueTask.CompletedTask;

    async ValueTask<T> ReadAsync<T>(Uuid tenantId, ImmutableContentRef reference,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info, CancellationToken ct) where T : class
    {
        var bytes = await content.ReadVerifiedAsync(tenantId.ToString(), reference, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(bytes.Span, info)
            ?? throw new ImmutableContentIntegrityException("The inventory register content is empty.");
    }
}
