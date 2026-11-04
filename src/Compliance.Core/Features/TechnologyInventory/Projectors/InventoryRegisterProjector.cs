using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Events;
using Bdgrz.Compliance.Features.TechnologyInventory.Projections;
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
    public async ValueTask<Result> HandleAsync(IProjectorContext<LocationRevisionRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, await ReadAsync(ev.TenantId, ev.ContentContent,
            ComplianceCoreJsonContext.Default.LocationContent, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        return Result.Success;
    }

    /// <inheritdoc />
    public async ValueTask<Result> HandleAsync(IProjectorContext<OperationalProcessRevisionRecorded> context,
        CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, await ReadAsync(ev.TenantId, ev.ContentContent,
            ComplianceCoreJsonContext.Default.OperationalProcessContent, ct).ConfigureAwait(false), ct)
            .ConfigureAwait(false);
        return Result.Success;
    }

    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(IProjectorContext<LocationChangeAccepted> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Success);

    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(IProjectorContext<LocationChangeCanceled> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Success);

    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(IProjectorContext<OperationalProcessChangeAccepted> context,
        CancellationToken ct) =>
        ValueTask.FromResult(Result.Success);

    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(IProjectorContext<OperationalProcessChangeCanceled> context,
        CancellationToken ct) =>
        ValueTask.FromResult(Result.Success);

    async ValueTask<T> ReadAsync<T>(Uuid tenantId, ImmutableContentRef reference,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info, CancellationToken ct) where T : class
    {
        var bytes = await content.ReadVerifiedAsync(tenantId.ToString(), reference, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(bytes.Span, info)
            ?? throw new ImmutableContentIntegrityException("The inventory register content is empty.");
    }
}
