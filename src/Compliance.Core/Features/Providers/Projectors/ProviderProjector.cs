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
using Bdgrz.Compliance.Features.Providers.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
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
    public async ValueTask<Result> HandleAsync(IProjectorContext<ProviderRecorded> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, await ReadAsync(ev.TenantId, ev.ContentContent, ct).ConfigureAwait(false), ct)
            .ConfigureAwait(false);
        return Result.Success;
    }

    /// <inheritdoc />
    public async ValueTask<Result> HandleAsync(IProjectorContext<ProviderRevised> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, await ReadAsync(ev.TenantId, ev.ContentContent, ct).ConfigureAwait(false), ct)
            .ConfigureAwait(false);
        return Result.Success;
    }

    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(IProjectorContext<ProviderChangeAccepted> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Success);

    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(IProjectorContext<ProviderChangeCanceled> context, CancellationToken ct) =>
        ValueTask.FromResult(Result.Success);

    async ValueTask<ProviderContent> ReadAsync(Uuid tenantId, Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef reference,
        CancellationToken ct)
    {
        var bytes = await content.ReadVerifiedAsync(tenantId.ToString(), reference, ct).ConfigureAwait(false);
        return System.Text.Json.JsonSerializer.Deserialize(bytes.Span, ComplianceCoreJsonContext.Default.ProviderContent)
            ?? throw new ImmutableContentIntegrityException("The provider content is empty.");
    }
}
