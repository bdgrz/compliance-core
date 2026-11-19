using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.History;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Projections;
namespace Bdgrz.Compliance.Features.Programs.Readers;

sealed class CassieClientServiceDirectory(CassieProjectionDatabase database, IImmutableContentStore content)
    : CassieProgramScopeRepository(database, "ClientServiceDirectory"),
        IClientServiceDirectoryReader, IClientServiceDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is IProductClientServiceFact native)
            ProductClientServiceHistory.ValidateHeader(native);
        switch (domainEvent)
        {
            case ClientServiceCreated created:
                var creation = await ProductClientServiceReader.ReadContentAsync(content, created, ct).ConfigureAwait(false);
                await InsertValueAsync(
                    new ClientServiceView(created.TenantId, created.ServiceId, 1, creation.Name,
                        creation.Purpose, creation.OwnerReference, "active", created.ActorMemberId,
                        created.ActorDisplay, created.ChangedAt,
                        created.ProgramId == Uuid.Empty ? null : created.ProgramId), ct).ConfigureAwait(false);
                await InsertRevisionAsync(
                        new ClientServiceRevisionView(created.ServiceId, 1, creation.Name,
                            creation.Purpose, creation.OwnerReference, "active", null,
                            created.ActorMemberId, created.ActorDisplay, created.ChangedAt,
                            created.ProgramId == Uuid.Empty ? null : created.ProgramId), ct)
                    .ConfigureAwait(false);
                break;
            case ClientServiceRevised revised:
                var revision = await ProductClientServiceReader.ReadContentAsync(content, revised, ct).ConfigureAwait(false);
                var current = await RequireCurrentAsync(revised.ServiceId, ct).ConfigureAwait(false);
                if (current.ProgramId != revised.ProgramId)
                    throw new ImmutableContentIntegrityException("The service revision differs from its actual projected predecessor.");
                await ReplaceValueAsync(
                    current with
                    {
                        Revision = revised.Revision,
                        Name = revision.Name,
                        Purpose = revision.Purpose,
                        OwnerReference = revision.OwnerReference,
                        LastChangedByMemberId = revised.ActorMemberId,
                        LastChangedByDisplay = revised.ActorDisplay,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                await InsertRevisionAsync(
                        new ClientServiceRevisionView(revised.ServiceId, revised.Revision, revision.Name,
                            revision.Purpose, revision.OwnerReference, "active", null,
                            revised.ActorMemberId, revised.ActorDisplay, revised.ChangedAt,
                            current.ProgramId), ct)
                    .ConfigureAwait(false);
                break;
            case ClientServiceRetired retired:
                var retirement = await ProductClientServiceReader.ReadRetirementAsync(content, retired, ct).ConfigureAwait(false);
                var retiredValue = await ProductClientServiceReader.ReadContentAsync(content, retired, ct).ConfigureAwait(false);
                var active = await RequireCurrentAsync(retired.ServiceId, ct).ConfigureAwait(false);
                if (active.ProgramId != retired.ProgramId || active.Name != retiredValue.Name ||
                    active.Purpose != retiredValue.Purpose || active.OwnerReference != retiredValue.OwnerReference)
                    throw new ImmutableContentIntegrityException("The service retirement differs from its actual projected predecessor.");
                await ReplaceValueAsync(
                    active with
                    {
                        Revision = retired.Revision,
                        Status = "retired",
                        LastChangedByMemberId = retired.ActorMemberId,
                        LastChangedByDisplay = retired.ActorDisplay,
                        LastChangedAt = retired.ChangedAt,
                    }, ct).ConfigureAwait(false);
                await InsertRevisionAsync(
                        new ClientServiceRevisionView(retired.ServiceId, retired.Revision, active.Name,
                            active.Purpose, active.OwnerReference, "retired", retirement.Rationale,
                            retired.ActorMemberId, retired.ActorDisplay, retired.ChangedAt,
                            active.ProgramId), ct)
                    .ConfigureAwait(false);
                break;
        }
    }

    async ValueTask<ClientServiceView> RequireCurrentAsync(Uuid serviceId, CancellationToken ct) =>
        await CurrentValueAsync(serviceId, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A service change cannot project before its creation.");

    public ValueTask<ClientServiceView?> GetAsync(Uuid tenantId, Uuid serviceId, CancellationToken ct = default) =>
        ReadAsync("client_service_directory", tenantId, serviceId.ToString(), ComplianceCoreJsonContext.Default.ClientServiceView, ct);

    public ValueTask<Page<ClientServiceView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) =>
        PageAsync("client_service_directory", tenantId, null, limit, cursor, ComplianceCoreJsonContext.Default.ClientServiceView, ct);

    public async ValueTask<Page<ClientServiceRevisionView>?> ListRevisionsAsync(Uuid tenantId, Uuid serviceId, int limit, string? cursor, CancellationToken ct = default)
    {
        if (await GetAsync(tenantId, serviceId, ct).ConfigureAwait(false) is null)
            return null;
        return await PageAsync("client_service_revisions", tenantId, serviceId.ToString(), limit, cursor,
            ComplianceCoreJsonContext.Default.ClientServiceRevisionView, ct).ConfigureAwait(false);
    }

    public ValueTask<ClientServiceRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid serviceId, long revision, CancellationToken ct = default) =>
        ReadAsync("client_service_revisions", tenantId, $"{serviceId}:{revision.ToString("D20", CultureInfo.InvariantCulture)}",
            ComplianceCoreJsonContext.Default.ClientServiceRevisionView, ct);

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("ClientServiceDirectory", EventStreamPattern.ForPattern(tenantId.ToString())), ct);

    ValueTask<ClientServiceView?> CurrentValueAsync(Uuid id, CancellationToken ct) =>
        CurrentAsync("client_service_directory", id.ToString(), ComplianceCoreJsonContext.Default.ClientServiceView, ct);
    ValueTask InsertValueAsync(ClientServiceView value, CancellationToken ct) =>
        InsertAsync("client_service_directory", value.TenantId, value.ServiceId.ToString(), (value.ProgramId ?? Uuid.Empty).ToString(),
            value.Name.ToUpperInvariant(), value, ComplianceCoreJsonContext.Default.ClientServiceView, ct);
    ValueTask ReplaceValueAsync(ClientServiceView value, CancellationToken ct) =>
        ReplaceAsync("client_service_directory", value.TenantId, value.ServiceId.ToString(), (value.ProgramId ?? Uuid.Empty).ToString(),
            value.Name.ToUpperInvariant(), value, ComplianceCoreJsonContext.Default.ClientServiceView, ct);
    ValueTask InsertRevisionAsync(ClientServiceRevisionView value, CancellationToken ct) =>
        InsertAsync("client_service_revisions", BatchTenant(), $"{value.ServiceId}:{value.Revision.ToString("D20", CultureInfo.InvariantCulture)}", value.ServiceId.ToString(),
            value.Revision.ToString("D20", CultureInfo.InvariantCulture), value, ComplianceCoreJsonContext.Default.ClientServiceRevisionView, ct);
    Uuid BatchTenant() => Uuid.Parse(Identity.Pattern.Realm, CultureInfo.InvariantCulture);
    public ValueTask<Page<ClientServiceView>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
        PageAsync("client_service_directory", tenantId, programId.ToString(), limit, cursor, ComplianceCoreJsonContext.Default.ClientServiceView, ct);
}
