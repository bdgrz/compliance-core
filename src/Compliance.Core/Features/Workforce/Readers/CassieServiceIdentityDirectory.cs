using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Projections;

namespace Bdgrz.Compliance.Features.Workforce.Readers;

/// <summary>
///     Projects governed non-human identities. Stored rows never carry ownership state: it depends
///     on the clock and the roster, so readers evaluate it on read.
/// </summary>
sealed class CassieServiceIdentityDirectory(CassieProjectionDatabase database)
    : CassieWorkforceRepository(database, "ServiceIdentityDirectoryV1", "service-identities"),
        IServiceIdentityDirectoryReader, IServiceIdentityDirectoryProjection
{
    public const string ManualSource = "manual";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("ServiceIdentityDirectoryV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "service-identities")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ServiceIdentityRecorded recorded:
                var terms = recorded.Terms;
                await InsertIdentityAsync(new ServiceIdentityView(recorded.TenantId, recorded.ServiceIdentityId, 1,
                        terms.DisplayName, terms.IdentityKind, terms.Purpose, terms.Environment,
                        terms.LifecycleStatus, terms.OwnerKind, terms.OwnerId, terms.ReviewBy,
                        ManualSource, false, [], recorded.Actor, recorded.ChangedAt, terms.ExpiresOn,
                        SharedJustification: terms.SharedJustification), ct)
                    .ConfigureAwait(false);
                break;
            case ServiceIdentityRevised revised:
                var current = await FindIdentityAsync(revised.ServiceIdentityId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A service identity revision cannot project before its predecessor.");
                await ReplaceIdentityAsync(current with
                    {
                        Revision = revised.Revision,
                        DisplayName = revised.Terms.DisplayName,
                        IdentityKind = revised.Terms.IdentityKind,
                        Purpose = revised.Terms.Purpose,
                        Environment = revised.Terms.Environment,
                        LifecycleStatus = revised.Terms.LifecycleStatus,
                        OwnerKind = revised.Terms.OwnerKind,
                        OwnerId = revised.Terms.OwnerId,
                        ReviewBy = revised.Terms.ReviewBy,
                        ExpiresOn = revised.Terms.ExpiresOn,
                        SharedJustification = revised.Terms.SharedJustification,
                        LastChangedBy = revised.Actor,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                break;
            case ServiceIdentityOwnerChanged owner:
                await ApplyNamedAsync(owner.TenantId, owner.ServiceIdentityId, owner.Revision, current =>
                {
                    if (current.OwnerKind != owner.PreviousOwnerKind || current.OwnerId != owner.PreviousOwnerId)
                        throw new InvalidOperationException("A named owner change requires the exact previous owner.");
                    return current with { OwnerKind = owner.OwnerKind, OwnerId = owner.OwnerId };
                }, owner.Actor, owner.ChangedAt, ct).ConfigureAwait(false);
                break;
            case ServiceIdentityPurposeChanged purpose:
                await ApplyNamedAsync(purpose.TenantId, purpose.ServiceIdentityId, purpose.Revision,
                    current => current with { Purpose = purpose.Purpose }, purpose.Actor, purpose.ChangedAt, ct).ConfigureAwait(false);
                break;
            case ServiceIdentityReviewed review:
                await ApplyNamedAsync(review.TenantId, review.ServiceIdentityId, review.Revision,
                    current => current with
                    {
                        ReviewBy = review.NextReviewDate,
                        LastReviewedOn = DateOnly.FromDateTime(review.ReviewedAt.UtcDateTime),
                        LastReviewer = review.Reviewer,
                        ReviewNotes = review.Notes,
                    }, review.Reviewer, review.ReviewedAt, ct).ConfigureAwait(false);
                break;
            case ServiceIdentityDisabled disabled:
                await ApplyNamedAsync(disabled.TenantId, disabled.ServiceIdentityId, disabled.Revision,
                    current => current with { LifecycleStatus = "disabled" }, disabled.Actor, disabled.ChangedAt, ct).ConfigureAwait(false);
                break;
            case ServiceIdentityEnabled enabled:
                await ApplyNamedAsync(enabled.TenantId, enabled.ServiceIdentityId, enabled.Revision,
                    current => current with { LifecycleStatus = "active" }, enabled.Actor, enabled.ChangedAt, ct).ConfigureAwait(false);
                break;
            case ServiceIdentityRetired retired:
                await ApplyNamedAsync(retired.TenantId, retired.ServiceIdentityId, retired.Revision,
                    current => current with { LifecycleStatus = "retired" }, retired.Actor, retired.ChangedAt, ct).ConfigureAwait(false);
                break;
        }
    }

    async ValueTask ApplyNamedAsync(Uuid tenantId, Uuid identityId, long revision,
        Func<ServiceIdentityView, ServiceIdentityView> change, ActorReference actor, DateTimeOffset changedAt, CancellationToken ct)
    {
        var current = await FindIdentityAsync(identityId, ct).ConfigureAwait(false);
        if (current is null || current.TenantId != tenantId || current.Revision + 1 != revision || current.LifecycleStatus == "retired")
            throw new InvalidOperationException("A named service identity fact requires its tenant's non-retired predecessor.");
        await ReplaceIdentityAsync(change(current) with
        {
            Revision = revision,
            LastChangedBy = actor,
            LastChangedAt = changedAt,
        }, ct).ConfigureAwait(false);
    }

    public async ValueTask<ServiceIdentityView?> GetAsync(Uuid tenantId, Uuid serviceIdentityId, CancellationToken ct = default)
    {
        var view = await ReadRowAsync("workforce_service_identities", tenantId, serviceIdentityId,
            ComplianceCoreJsonContext.Default.ServiceIdentityView, ct).ConfigureAwait(false);
        return view?.TenantId == tenantId ? view : null;
    }

    public ValueTask<Page<ServiceIdentityView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default) =>
        ReadPageAsync("workforce_service_identities", tenantId, ComplianceCoreJsonContext.Default.ServiceIdentityView, limit, cursor, "all", ct);

    ValueTask<ServiceIdentityView?> FindIdentityAsync(Uuid id, CancellationToken ct) =>
        FindCurrentAsync("workforce_service_identities", id, ComplianceCoreJsonContext.Default.ServiceIdentityView, ct);

    ValueTask InsertIdentityAsync(ServiceIdentityView row, CancellationToken ct) =>
        InsertRowAsync("workforce_service_identities", row.TenantId, row.ServiceIdentityId, row,
            ComplianceCoreJsonContext.Default.ServiceIdentityView, ct, ("sort_key", row.DisplayName.ToUpperInvariant()));

    ValueTask ReplaceIdentityAsync(ServiceIdentityView row, CancellationToken ct) =>
        ReplaceRowAsync("workforce_service_identities", row.TenantId, row.ServiceIdentityId, row,
            ComplianceCoreJsonContext.Default.ServiceIdentityView, ct, ("sort_key", row.DisplayName.ToUpperInvariant()));
}
